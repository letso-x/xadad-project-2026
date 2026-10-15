using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MzukuluQMSMobile.Models;
using MzukuluQMSMobile.Services;

namespace MzukuluQMSMobile.ViewModels;

public sealed class ProjectsViewModel : INotifyPropertyChanged
{
    private readonly IProjectService _projectService;

    private bool _isBusy;
    private string? _errorMessage;

    public ObservableCollection<Project> Projects { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoad));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool CanLoad => !IsBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProjectsViewModel(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var projects =
                await _projectService.GetProjectsAsync(
                    cancellationToken);

            Projects.Clear();

            foreach (var project in projects)
            {
                Projects.Add(project);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not an application error.
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}