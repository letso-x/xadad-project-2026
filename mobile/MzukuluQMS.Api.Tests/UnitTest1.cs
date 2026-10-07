using Moq;
using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Tests;

public sealed class ProjectServiceTests
{
    private readonly Mock<IProjectRepository> _repositoryMock;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _repositoryMock = new Mock<IProjectRepository>();
        _service = new ProjectService(_repositoryMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CallsRepository()
    {
        var request = new CreateProjectRequest
        {
            ClientID = 1,
            ProjectNumber = " PROJ-TEST ",
            ProjectName = " Test Project ",
            Description = " Test description "
        };

        var expectedProject = new ProjectDto
        {
            ProjectID = 10,
            ClientID = 1,
            ProjectNumber = "PROJ-TEST",
            ProjectName = "Test Project",
            Description = "Test description",
            IsActive = true,
            Status = "Planning"
        };

        _repositoryMock
            .Setup(repository => repository.CreateAsync(
                It.IsAny<CreateProjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProject);

        var result = await _service.CreateAsync(request);

        Assert.Equal(expectedProject.ProjectID, result.ProjectID);

        _repositoryMock.Verify(
            repository => repository.CreateAsync(
                It.Is<CreateProjectRequest>(r =>
                    r.ProjectNumber == "PROJ-TEST" &&
                    r.ProjectName == "Test Project" &&
                    r.Description == "Test description"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidClientId_ThrowsArgumentException()
    {
        var request = new CreateProjectRequest
        {
            ClientID = 0,
            ProjectName = "Test Project"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request));

        _repositoryMock.Verify(
            repository => repository.CreateAsync(
                It.IsAny<CreateProjectRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithMissingProjectName_ThrowsArgumentException()
    {
        var request = new CreateProjectRequest
        {
            ClientID = 1,
            ProjectName = "   "
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request));

        _repositoryMock.Verify(
            repository => repository.CreateAsync(
                It.IsAny<CreateProjectRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithEndDateBeforeStartDate_ThrowsArgumentException()
    {
        var request = new CreateProjectRequest
        {
            ClientID = 1,
            ProjectName = "Test Project",
            StartDate = new DateTime(2026, 10, 10),
            EndDate = new DateTime(2026, 10, 9)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request));

        _repositoryMock.Verify(
            repository => repository.CreateAsync(
                It.IsAny<CreateProjectRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}