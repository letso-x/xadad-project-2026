using System.Buffers.Binary;
using System.IO.Compression;
namespace MzukuluQMS.Mobile.Core;
public readonly record struct InkPoint(float X,float Y);
// Normalised strokes are independent of screen density/orientation. No typed name is treated as a signoff.
public sealed class SignatureInk
{
 private readonly List<List<InkPoint>> strokes=[];
 public IReadOnlyList<List<InkPoint>> Strokes=>strokes;
 public bool HasInk=>strokes.Any(s=>s.Count>1);
 public void Begin(float x,float y){strokes.Add([]);Move(x,y);}
 public void Move(float x,float y){if(strokes.Count>0)strokes[^1].Add(new(Math.Clamp(x,0,1),Math.Clamp(y,0,1)));}
 public void Clear()=>strokes.Clear();
 public byte[] ToPng(int width=600,int height=240)
 {
  if(!HasInk)throw new InvalidOperationException("Draw your signature before saving.");
  if(width<32 || height<32 || width>2000 || height>2000)throw new ArgumentOutOfRangeException(nameof(width));
  var pixels=Enumerable.Repeat((byte)255,width*height*4).ToArray();
  void Dot(int x,int y){for(var oy=-2;oy<=2;oy++)for(var ox=-2;ox<=2;ox++){var px=x+ox;var py=y+oy;if(px<0 || py<0 || px>=width || py>=height || ox*ox+oy*oy>4)continue;var i=(py*width+px)*4;pixels[i]=15;pixels[i+1]=31;pixels[i+2]=51;}}
  foreach(var stroke in strokes)for(var i=1;i<stroke.Count;i++)
  {
   var a=stroke[i-1];var b=stroke[i];var dx=(b.X-a.X)*(width-1);var dy=(b.Y-a.Y)*(height-1);var steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(dx),Math.Abs(dy))));
   for(var j=0;j<=steps;j++)Dot((int)Math.Round(a.X*(width-1)+dx*j/steps),(int)Math.Round(a.Y*(height-1)+dy*j/steps));
  }
  using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
  var header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=6;Chunk(output,"IHDR",header);
  using var compressed=new MemoryStream();using(var zip=new ZLibStream(compressed,CompressionLevel.Optimal,true))for(var y=0;y<height;y++){zip.WriteByte(0);zip.Write(pixels,y*width*4,width*4);}
  Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);return output.ToArray();
 }
 private static void Chunk(Stream output,string type,byte[] data)
 {
  Span<byte> number=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(number,data.Length);output.Write(number);
  var bytes=System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();output.Write(bytes);
  uint crc=0xffffffff;foreach(var b in bytes){crc^=b;for(var i=0;i<8;i++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}
  BinaryPrimitives.WriteUInt32BigEndian(number,~crc);output.Write(number);
 }
}
