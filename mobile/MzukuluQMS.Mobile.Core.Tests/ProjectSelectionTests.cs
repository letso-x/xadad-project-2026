using MzukuluQMS.Mobile.Core;

namespace MzukuluQMS.Mobile.Core.Tests;

public sealed class ProjectSelectionTests
{
    [Fact]
    public void SelectionUsesIdWhenNamesAreDuplicatedAndListIsReordered()
    {
        var selection = new ProjectSelection();
        ProjectChoice[] projects = [new(13,"Site","P-13"),new(14,"Site","P-14")];
        selection.Refresh(projects);
        Assert.Null(selection.Selected);
        selection.Select(14);
        selection.Refresh(projects.Reverse());
        Assert.Equal(14L,selection.Selected!.Id);
        Assert.Equal("Site · P-14",selection.Selected.DisplayName);
    }

    [Fact]
    public void RefreshClearsRemovedSelectionAndRejectsUnavailableProject()
    {
        var selection = new ProjectSelection(13);
        selection.Refresh([new(13,"Site",null)]);
        Assert.Equal(13L,selection.Selected!.Id);
        selection.Refresh([]);
        Assert.Null(selection.Selected);
        Assert.Throws<ArgumentException>(()=>selection.Select(13));
        selection.Refresh([new(14,"Other site",null)]);
        Assert.Null(selection.Selected);
    }
}
