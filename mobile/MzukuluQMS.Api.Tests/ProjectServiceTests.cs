using Moq;
using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Tests;

public sealed class ProjectServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepository;
    private readonly Mock<IClientRepository> _clientRepository;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _projectRepository =
            new Mock<IProjectRepository>();

        _clientRepository =
            new Mock<IClientRepository>();

        _service =
            new ProjectService(
                _projectRepository.Object,
                _clientRepository.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CallsRepository()
    {
        var request =
            new CreateProjectRequest
            {
                ClientID = 1,
                ProjectNumber = "PRJ-001",
                ProjectName = "Test Project"
            };

        var expectedProject =
            new ProjectDto
            {
                ProjectID = 1,
                ClientID = 1,
                ProjectNumber = "PRJ-001",
                ProjectName = "Test Project",
                IsActive = true,
                Status = "Planning"
            };

        _clientRepository
            .Setup(repository =>
                repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ClientDto
                {
                    ClientID = 1,
                    ClientName = "Test Client",
                    IsActive = true
                });

        _projectRepository
            .Setup(repository =>
                repository.CreateAsync(
                    It.IsAny<CreateProjectRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProject);

        var result =
            await _service.CreateAsync(request);

        Assert.Equal(
            expectedProject.ProjectID,
            result.ProjectID);

        Assert.Equal(
            expectedProject.ProjectName,
            result.ProjectName);

        _projectRepository.Verify(
            repository =>
                repository.CreateAsync(
                    It.Is<CreateProjectRequest>(
                        value =>
                            value.ClientID == 1 &&
                            value.ProjectNumber == "PRJ-001" &&
                            value.ProjectName == "Test Project"),
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidClientId_ThrowsArgumentException()
    {
        var request =
            new CreateProjectRequest
            {
                ClientID = 0,
                ProjectName = "Test Project"
            };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(request));

        _clientRepository.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _projectRepository.Verify(
            repository =>
                repository.CreateAsync(
                    It.IsAny<CreateProjectRequest>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithMissingProjectName_ThrowsArgumentException()
    {
        var request =
            new CreateProjectRequest
            {
                ClientID = 1,
                ProjectName = ""
            };

        _clientRepository
            .Setup(repository =>
                repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ClientDto
                {
                    ClientID = 1,
                    ClientName = "Test Client",
                    IsActive = true
                });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(request));

        _projectRepository.Verify(
            repository =>
                repository.CreateAsync(
                    It.IsAny<CreateProjectRequest>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithEndDateBeforeStartDate_ThrowsArgumentException()
    {
        var request =
            new CreateProjectRequest
            {
                ClientID = 1,
                ProjectName = "Test Project",
                StartDate = new DateTime(2026, 10, 10),
                EndDate = new DateTime(2026, 10, 9)
            };

        _clientRepository
            .Setup(repository =>
                repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ClientDto
                {
                    ClientID = 1,
                    ClientName = "Test Client",
                    IsActive = true
                });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(request));

        _projectRepository.Verify(
            repository =>
                repository.CreateAsync(
                    It.IsAny<CreateProjectRequest>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithMissingClient_ThrowsKeyNotFoundException()
    {
        var request =
            new CreateProjectRequest
            {
                ClientID = 99,
                ProjectName = "Test Project"
            };

        _clientRepository
            .Setup(repository =>
                repository.GetByIdAsync(
                    99,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDto?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(request));

        _projectRepository.Verify(
            repository =>
                repository.CreateAsync(
                    It.IsAny<CreateProjectRequest>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveClient_ThrowsInvalidOperationException()
    {
        var request =
            new CreateProjectRequest
            {
                ClientID = 1,
                ProjectName = "Test Project"
            };

        _clientRepository
            .Setup(repository =>
                repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ClientDto
                {
                    ClientID = 1,
                    ClientName = "Inactive Client",
                    IsActive = false
                });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(request));

        _projectRepository.Verify(
            repository =>
                repository.CreateAsync(
                    It.IsAny<CreateProjectRequest>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }
}