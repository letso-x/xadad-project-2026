using Moq;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Exceptions;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class ItpRulesTests
{
 private static readonly Guid Author=Guid.NewGuid(),Other=Guid.NewGuid();
 private static readonly DateTimeOffset Now=DateTimeOffset.UtcNow;
 private static ItpStep Step(long id,string point="None",params long[] deps)=>new(id,(int)id,"A"+id,"Activity "+id,"Work","Check criteria",point,deps.ToList());
 private static ItpVersion Version(string state="Approved",params ItpStep[] steps)
 {
  if(steps.Length==0)steps=[Step(1,"Hold"),Step(2,"Witness",1),Step(3,"Review",2)];
  return new(1,1,1,state,0,state=="Approved",new("ITP-1","Plan",steps.ToList()),Author,Now,state=="Draft"?null:Author,state=="Draft"?null:Now,state=="Approved"?Other:null,state=="Approved"?Now:null,steps.Select(s=>new ItpExecution(s.Id)).ToList());
 }
 private static ItpVersion Act(ItpVersion v,string action,long id,Guid? who=null,bool qc=true)=>ItpRules.Execute(v,action,new(v.Stamp,id,"Criteria checked"),who??Author,Now,qc);
 [Theory][InlineData("cycle")][InlineData("self")][InlineData("missing")][InlineData("duplicate")]
 public void Invalid_dependency_graph_rejected(string kind)
 {
  var a=kind switch{"cycle"=>new[]{Step(1,"None",2),Step(2,"None",1)},"self"=>new[]{Step(1,"None",1)},"missing"=>new[]{Step(1,"None",9)},_=>new[]{Step(1),Step(1)}};
  Assert.Throws<ArgumentException>(()=>ItpRules.Validate(new("ITP","Plan",a.ToList())));
 }
 [Fact]public void Predecessor_and_unreleased_hold_block_successor_even_after_completion()
 {
  var v=Version();Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",2));v=Act(v,"complete",1);
  Assert.Contains("A1",ItpRules.BlockedReason(v,v.Definition.Activities[1]));Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",2));Assert.Throws<InvalidOperationException>(()=>Act(v,"release",1));
  v=Act(v,"release",1,Other);Assert.Null(ItpRules.BlockedReason(v,v.Definition.Activities[1]));Assert.NotNull(Act(v,"complete",2).Executions[1].CompletedAt);
 }
 [Fact]public void Hold_requires_completion_records_actor_time_comments_and_cannot_release_twice()
 {
  var v=Version();Assert.Throws<InvalidOperationException>(()=>Act(v,"release",1,Other));v=Act(Act(v,"complete",1),"release",1,Other);
  Assert.Equal(Other,v.Executions[0].ReleasedBy);Assert.Equal(Now,v.Executions[0].ReleasedAt);Assert.Equal("Criteria checked",v.Executions[0].ReleaseComments);Assert.Throws<InvalidOperationException>(()=>Act(v,"release",1,Other));
 }
 [Fact]public void Witness_and_review_record_traceability_and_gate_successors()
 {
  var v=Version("Approved",Step(1,"Witness"),Step(2,"Review",1),Step(3,"None",2));v=Act(v,"complete",1);Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",2));v=Act(v,"witness",1,Other);
  Assert.Equal(Other,v.Executions[0].WitnessedBy);Assert.Equal(Now,v.Executions[0].WitnessedAt);v=Act(v,"complete",2);Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",3));v=Act(v,"review",2,Other);
  Assert.Equal(Other,v.Executions[1].ReviewedBy);Assert.Equal(Now,v.Executions[1].ReviewedAt);Assert.NotNull(Act(v,"complete",3).Executions[2].CompletedAt);
 }
 [Fact]public void Witness_requirement_is_configurable_without_silent_acknowledgement()
 {
  var v=Act(Version("Approved",Step(1,"Witness") with {WitnessRequired=false},Step(2,"None",1)),"complete",1);v=Act(v,"complete",2);Assert.Null(v.Executions[0].WitnessedAt);Assert.Equal(0,ItpRules.Progress(v).WitnessPending);
 }
 [Fact]public void Draft_submitted_and_approved_editability_and_approval()
 {
  var v=Version("Draft");v=ItpRules.SaveDraft(v,v.Definition with {Title="Revised"});Assert.Equal(1,v.Stamp);v=ItpRules.Transition(v,"submit",Author,Now);Assert.Equal(Author,v.SubmittedBy);Assert.Equal(Now,v.SubmittedAt);
  Assert.Throws<InvalidOperationException>(()=>ItpRules.SaveDraft(v,v.Definition));Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",1));v=ItpRules.Transition(v,"approve",Other,Now);Assert.True(v.IsCurrent);Assert.Equal(Other,v.ApprovedBy);Assert.Throws<InvalidOperationException>(()=>ItpRules.SaveDraft(v,v.Definition));Assert.Throws<InvalidOperationException>(()=>ItpRules.Transition(v,"approve",Other,Now));
 }
 [Fact]public void Empty_structure_and_unconfigured_hold_block_submission_or_approval()
 {
  var empty=Version("Draft") with {Definition=new("ITP","Plan",[])};Assert.Throws<ArgumentException>(()=>ItpRules.Transition(empty,"submit",Author,Now));
  Assert.Throws<ArgumentException>(()=>ItpRules.Transition(Version("Submitted",Step(1,"Hold") with {Criteria=""}),"approve",Author,Now));Assert.Throws<ArgumentException>(()=>ItpRules.Validate(new("ITP","Plan",[Step(1) with {RequireQc=true}])));
 }
 [Fact]public void Historical_version_read_only_and_new_revision_has_fresh_execution()
 {
  var historical=Act(Version(),"complete",1);Assert.Throws<InvalidOperationException>(()=>Act(historical with {IsCurrent=false},"release",1,Other));
  var revision=ItpRules.SaveDraft(Version("Draft") with {Id=2,Revision=2,Definition=historical.Definition},historical.Definition);Assert.NotNull(historical.Executions[0].CompletedAt);Assert.All(revision.Executions,e=>Assert.Null(e.CompletedAt));Assert.Equal(1,historical.Revision);Assert.Equal(2,revision.Revision);
 }
 [Fact]public void QC_approval_does_not_release_hold_and_unacceptable_QC_blocks_progress()
 {
  var v=Version("Approved",Step(1,"Hold") with {RequireQc=true,ChecklistTemplateVersionId=5},Step(2,"None",1));Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",1,qc:false));v=Act(v,"complete",1);Assert.Null(v.Executions[0].ReleasedAt);Assert.Throws<InvalidOperationException>(()=>Act(v,"complete",2));Assert.Throws<InvalidOperationException>(()=>Act(v,"release",1,Other,false));v=Act(v,"release",1,Other);Assert.Null(ItpRules.BlockedReason(v,v.Definition.Activities[1]));
 }
 [Fact]public void Progress_keeps_gate_and_completion_metrics_separate()
 {
  var p=ItpRules.Progress(Act(Version(),"complete",1));Assert.Equal(3,p.TotalActivities);Assert.Equal(1,p.CompletedActivities);Assert.Equal(2,p.BlockedActivities);Assert.Equal(1,p.OpenHolds);Assert.Equal(0,p.ReleasedHolds);Assert.Equal(1,p.WitnessPending);Assert.Equal(1,p.ReviewPending);Assert.Equal(33.3m,p.PercentageComplete);
 }
 [Theory][InlineData("save","ManageItp")][InlineData("revise","ManageItp")][InlineData("submit","SubmitItp")][InlineData("approve","ApproveItp")][InlineData("complete","ExecuteItp")][InlineData("release","ReleaseItpHold")][InlineData("witness","WitnessItp")][InlineData("review","ReviewItp")][InlineData("link-qc","ExecuteItp")]
 public async Task Every_write_enforces_existing_project_permission_boundary(string action,string permission)
 {
  var workflow=new Mock<IQCWorkflow>(MockBehavior.Strict);workflow.Setup(w=>w.BeginAsync(null,42,permission,It.IsAny<CancellationToken>())).ThrowsAsync(new ForbiddenException("denied"));await using var db=new QmsDbSession(Mock.Of<IDbConnectionFactory>());var service=new ItpService(db,workflow.Object,Mock.Of<ICurrentUserService>(),Mock.Of<IQCFormContentHashService>());
  await Assert.ThrowsAsync<ForbiddenException>(async()=>{if(action=="save")await service.Save(42,7,new(0,Version().Definition),default);else await service.Act(42,7,action,new(0,1,"Checked"),default);});workflow.VerifyAll();Assert.Null(db.Connection);
 }
 [Theory][InlineData("list")][InlineData("get")][InlineData("audit")]
 public async Task Reads_enforce_project_access(string action)
 {
  var workflow=new Mock<IQCWorkflow>();workflow.Setup(w=>w.BeginAsync(null,99,"ReadItp",It.IsAny<CancellationToken>())).ThrowsAsync(new ForbiddenException("denied"));await using var db=new QmsDbSession(Mock.Of<IDbConnectionFactory>());var service=new ItpService(db,workflow.Object,Mock.Of<ICurrentUserService>(),Mock.Of<IQCFormContentHashService>());
  await Assert.ThrowsAsync<ForbiddenException>(async()=>{if(action=="list")await service.List(99,default);else if(action=="get")await service.Get(99,1,default);else await service.AuditHistory(99,1,default);});workflow.VerifyAll();
 }
}
