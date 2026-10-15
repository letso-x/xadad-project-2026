using System.Text.Json;
using Moq;
using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.DTOs.Users;
using MzukuluQMS.Api.DTOs.SignOffs;
using MzukuluQMS.Api.Services;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.Workflow;
using MzukuluQMS.Api.Exceptions;
namespace MzukuluQMS.Api.Tests;

public sealed class QCWorkflowServiceTests
{
    private readonly Mock<IQCFormRepository> repo = new();
    private readonly Mock<IQCWorkflow> workflow = new();
    private readonly Mock<IQCOperation> operation = new();
    private readonly Mock<ICurrentUserService> users = new();
    private readonly Mock<IQCFormContentHashService> hash = new();
    private QCFormService Service => new(repo.Object, new FieldValueValidator(), users.Object, workflow.Object, hash.Object, new NcrService(null!,workflow.Object,users.Object));
    public QCWorkflowServiceTests()
    {
        workflow.Setup(w => w.BeginAsync(It.IsAny<long?>(),It.IsAny<long?>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(operation.Object);
        users.Setup(u => u.GetCurrentUserAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CurrentUserDto { UserID=Guid.NewGuid(), IsActive=true });
        hash.Setup(h => h.GenerateHashAsync(101,It.IsAny<CancellationToken>())).ReturnsAsync(new string('a',64));
    }
    [Theory]
    [InlineData("Draft")][InlineData("InProgress")][InlineData("ReturnedForCorrection")]
    public async Task Editable_states_accept_valid_response_and_audit(string status)
    {
        repo.Setup(r => r.GetStatusAsync(101,default)).ReturnsAsync(status);
        repo.Setup(r => r.GetResponseValidationAsync(101,201,default)).ReturnsAsync(new QCResponseValidationRow { FieldType="Text", IsRequired=true });
        await Service.UpdateResponseAsync(101,201,new UpdateQCResponseRequest { Value=JsonSerializer.SerializeToElement("corrected") });
        repo.Verify(r => r.UpdateResponseAsync(101,201,"\"corrected\"",It.IsAny<Guid?>(),default),Times.Once);
        operation.Verify(o => o.CompleteAsync("QCResponseUpdated",101,It.IsAny<object>(),default,null),Times.Once);
    }
    [Theory]
    [InlineData("Submitted")][InlineData("Approved")][InlineData("Cancelled")][InlineData("Rejected")]
    public async Task Locked_states_never_write_response(string status)
    {
        repo.Setup(r => r.GetStatusAsync(101,default)).ReturnsAsync(status);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.UpdateResponseAsync(101,201,new UpdateQCResponseRequest { Value=JsonSerializer.SerializeToElement("change") }));
        repo.Verify(r=>r.UpdateResponseAsync(It.IsAny<long>(),It.IsAny<long>(),It.IsAny<string>(),It.IsAny<Guid?>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Theory]
    [InlineData("Draft")][InlineData("InProgress")][InlineData("ReturnedForCorrection")]
    public async Task Submit_allows_editable_states_without_pre_submit_signoff(string status)
    {
        repo.Setup(r => r.GetStatusAsync(101,default)).ReturnsAsync(status);
        await Service.SubmitAsync(101);
        repo.Verify(r=>r.SubmitAsync(101,default),Times.Once);
        workflow.Verify(w=>w.EnsureCurrentSignaturesAsync(It.IsAny<long>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Theory]
    [InlineData(1,0,0)][InlineData(0,1,0)][InlineData(0,0,1)]
    public async Task Missing_required_data_prevents_submission(int fields,int responses,int photos)
    {
        repo.Setup(r=>r.GetStatusAsync(101,default)).ReturnsAsync("Draft");
        repo.Setup(r=>r.CountMissingRequiredProjectFieldsAsync(101,default)).ReturnsAsync(fields);
        repo.Setup(r=>r.CountMissingRequiredResponsesAsync(101,default)).ReturnsAsync(responses);
        repo.Setup(r=>r.CountMissingRequiredPhotoEvidenceAsync(101,default)).ReturnsAsync(photos);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SubmitAsync(101));
        repo.Verify(r=>r.SubmitAsync(It.IsAny<long>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]
    public async Task Stale_or_missing_current_signature_prevents_approval()
    {
        repo.Setup(r=>r.GetStatusAsync(101,default)).ReturnsAsync("Submitted");
        workflow.Setup(w=>w.EnsureCurrentSignaturesAsync(101,new string('a',64),default)).ThrowsAsync(new InvalidOperationException("Stale signature"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.ApproveAsync(101));
        repo.Verify(r=>r.ApproveAsync(It.IsAny<long>(),It.IsAny<CancellationToken>()),Times.Never);
        operation.Verify(o=>o.CompleteAsync(It.IsAny<string>(),It.IsAny<long>(),It.IsAny<object>(),It.IsAny<CancellationToken>(),null),Times.Never);
    }
    [Fact]
    public async Task Current_signatures_checked_before_approval_and_audit()
    {
        repo.Setup(r=>r.GetStatusAsync(101,default)).ReturnsAsync("Submitted");
        await Service.ApproveAsync(101);
        workflow.Verify(w=>w.EnsureCurrentSignaturesAsync(101,new string('a',64),default),Times.Once);
        repo.Verify(r=>r.ApproveAsync(101,default),Times.Once);
        operation.Verify(o=>o.CompleteAsync("QCFormApproved",101,null,default,null),Times.Once);
    }
    [Fact]
    public async Task Permission_denial_prevents_mutation()
    {
        workflow.Setup(w=>w.BeginAsync(101,null,"ApproveInspection",default)).ThrowsAsync(new ForbiddenException("Denied"));
        await Assert.ThrowsAsync<ForbiddenException>(()=>Service.ApproveAsync(101));
        repo.Verify(r=>r.GetStatusAsync(It.IsAny<long>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]
    public async Task Correction_records_reason_and_does_not_delete_signatures()
    {
        repo.Setup(r=>r.GetStatusAsync(101,default)).ReturnsAsync("Submitted");
        await Service.ReturnForCorrectionAsync(101,"Fix drawing number");
        repo.Verify(r=>r.ReturnForCorrectionAsync(101,default),Times.Once);
        operation.Verify(o=>o.CompleteAsync("QCFormReturnedForCorrection",101,It.Is<object>(d=>d.ToString()!.Contains("Fix drawing number")),default,null),Times.Once);
    }
    [Fact]
    public async Task Correction_requires_reason()
    {
        await Assert.ThrowsAsync<ArgumentException>(()=>Service.ReturnForCorrectionAsync(101," "));
        repo.Verify(r=>r.ReturnForCorrectionAsync(It.IsAny<long>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]
    public async Task Audit_failure_is_not_reported_as_success()
    {
        repo.Setup(r=>r.GetStatusAsync(101,default)).ReturnsAsync("Submitted");
        operation.Setup(o=>o.CompleteAsync("QCFormApproved",101,null,default,null)).ThrowsAsync(new Exception("Audit unavailable"));
        await Assert.ThrowsAsync<Exception>(()=>Service.ApproveAsync(101));
        operation.Verify(o=>o.DisposeAsync(),Times.Once);
    }
}
