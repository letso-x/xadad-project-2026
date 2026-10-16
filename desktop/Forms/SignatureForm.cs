using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace MzuApplication.Forms
{
 internal sealed class SignatureForm:Form
 {
  private readonly Bitmap image=new Bitmap(640,240);
  private Point? previous;private bool hasInk;
  public byte[] Png {get;private set;}
  public SignatureForm()
  {
   Text="Draw your signature";StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(664,306);Font=Theme.Body;AppIcon.Apply(this);
   using(var g=Graphics.FromImage(image))g.Clear(Color.White);
   var canvas=new PictureBox{Image=image,Location=new Point(12,12),Size=new Size(640,240),BorderStyle=BorderStyle.FixedSingle};Controls.Add(canvas);
   canvas.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){previous=e.Location;canvas.Capture=true;}};
   canvas.MouseMove+=(s,e)=>{if(previous.HasValue && e.Button==MouseButtons.Left){using(var g=Graphics.FromImage(image))using(var pen=new Pen(Color.Black,3)){g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.DrawLine(pen,previous.Value,e.Location);}previous=e.Location;hasInk=true;canvas.Invalidate();}};
   canvas.MouseUp+=(s,e)=>{previous=null;canvas.Capture=false;};
   var clear=new Button{Text="Clear",Location=new Point(12,266)};clear.Click+=(s,e)=>{using(var g=Graphics.FromImage(image))g.Clear(Color.White);hasInk=false;canvas.Invalidate();};Controls.Add(clear);
   var done=new Button{Text="Use signature",Location=new Point(510,266),Width=140};done.Click+=(s,e)=>{if(!hasInk)return;using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);Png=stream.ToArray();}DialogResult=DialogResult.OK;Close();};Controls.Add(done);
  }
  protected override void Dispose(bool disposing){if(disposing)image.Dispose();base.Dispose(disposing);}
 }
}
