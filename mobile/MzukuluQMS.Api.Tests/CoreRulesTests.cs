using MzukuluQMS.Api.Services.Workflow;
using MzukuluQMS.Api.Exceptions;
namespace MzukuluQMS.Api.Tests;
public sealed class CoreRulesTests
{
 [Fact]public void Cycles_and_missing_dependencies_are_rejected()
 {Assert.Throws<ArgumentException>(()=>CoreRules.ValidateDependencies(new[]{new ItpActivity(1,new long[]{2},false,false,false),new ItpActivity(2,new long[]{1},false,false,false)}));Assert.Throws<ArgumentException>(()=>CoreRules.ValidateDependencies(new[]{new ItpActivity(1,new long[]{99},false,false,false)}));}
 [Fact]public void Hold_and_completion_prevent_downstream_progress()
 {var rows=new[]{new ItpActivity(1,Array.Empty<long>(),true,true,false),new ItpActivity(2,new long[]{1},false,false,false)};Assert.Throws<InvalidOperationException>(()=>CoreRules.RequireProgressPrerequisites(2,rows));CoreRules.RequireProgressPrerequisites(2,new[]{rows[0] with {HoldReleased=true},rows[1]});Assert.Throws<ForbiddenException>(()=>CoreRules.RequireHoldRelease(1,rows,false,"release"));CoreRules.RequireHoldRelease(1,rows,true,"checked");}
 [Fact]public void Calibration_due_date_is_inclusive_and_future_or_expired_is_denied()
 {var calibrated=new DateOnly(2026,1,1);var due=new DateOnly(2026,10,12);CoreRules.RequireValidCalibration(calibrated,due,due,true);Assert.Throws<InvalidOperationException>(()=>CoreRules.RequireValidCalibration(calibrated,due,due.AddDays(1),true));Assert.Throws<InvalidOperationException>(()=>CoreRules.RequireValidCalibration(calibrated,due,calibrated.AddDays(-1),true));Assert.Throws<InvalidOperationException>(()=>CoreRules.RequireValidCalibration(calibrated,due,due,false));}
}
