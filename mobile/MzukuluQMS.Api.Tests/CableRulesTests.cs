using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class CableRulesTests
{
 private static CableInput Cable()=>new("C-1","Source","Destination","XLPE","4 mm²","4","D-1",50,true,true,true,"IR-1","CT-1",true,0);
 [Fact] public void Complete_cable_requires_every_stage_and_both_certificates()
 {
  var r=Cable();CableRules.Validate(r);
  Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {Pulled=null}));
  Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {Glanded=false}));
  Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {Terminated=false}));
  Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {IrCertificateNumber=""}));
  Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {ContinuityCertificateNumber=null}));
 }
 [Fact] public void Historical_unknown_stages_are_not_invented_or_forced_complete()=>CableRules.Validate(Cable() with {Pulled=null,Glanded=null,Terminated=null,Complete=null,IrCertificateNumber=null,ContinuityCertificateNumber=null});
 [Fact] public void Negative_length_and_stale_revision_shape_are_rejected()
 {Assert.Throws<ArgumentException>(()=>CableRules.Validate(Cable() with {Length=-1}));Assert.Throws<ArgumentException>(()=>CableRules.Validate(Cable() with {ExpectedRevision=-1}));}
 [Fact] public void Header_requires_business_number_and_bounded_notes()
 {
  var r=new CableRegisterInput("QC-CABLE-1",null,"Rev 1",null,null,null,null,0);CableRules.Validate(r);
  Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {RegisterNumber=""}));Assert.Throws<ArgumentException>(()=>CableRules.Validate(r with {Notes=new string('x',4001)}));
 }
}
