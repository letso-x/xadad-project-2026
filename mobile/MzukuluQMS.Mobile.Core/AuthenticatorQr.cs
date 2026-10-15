using System.Xml;
using System.Xml.Linq;
namespace MzukuluQMS.Mobile.Core;
public static class AuthenticatorQr
{
 public static string Html(string qr)
 {
  try
  {
   if(qr.StartsWith("data:image/svg+xml",StringComparison.OrdinalIgnoreCase))
   {
    var comma=qr.IndexOf(',');var header=qr[..comma];var data=qr[(comma+1)..];
    qr=header.EndsWith(";base64",StringComparison.OrdinalIgnoreCase)?System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(data)):Uri.UnescapeDataString(data);
   }
   if(qr.Length>1024*1024)throw new FormatException();
   using var reader=XmlReader.Create(new StringReader(qr),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});
   var svg=XDocument.Load(reader).Root??throw new FormatException();
   var elements=new HashSet<string>{"svg","g","path","rect","title","desc"};
   var attributes=new HashSet<string>{"viewBox","width","height","fill","stroke","stroke-width","d","x","y","shape-rendering","version","transform"};
   // Supabase's goqrsvg output uses style="fill:black;stroke:none" on each rect.
   // Normalize only these inert paints; never permit arbitrary CSS or URLs.
   foreach(var element in svg.DescendantsAndSelf())
   {
    if(element.Attribute("style") is not { } style)continue;
    foreach(var declaration in style.Value.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
    {
     var pair=declaration.Split(':',2,StringSplitOptions.TrimEntries);
     if(pair.Length!=2||pair[0] is not ("fill" or "stroke")||pair[1] is not ("black" or "white" or "none"))throw new FormatException();
     element.SetAttributeValue(pair[0],pair[1]);
    }
    style.Remove();
   }
   if(svg.Name.LocalName!="svg"||svg.DescendantsAndSelf().Any(e=>!elements.Contains(e.Name.LocalName)||e.Attributes().Any(a=>!a.IsNamespaceDeclaration&&(!attributes.Contains(a.Name.LocalName)||a.Value.Contains("url(",StringComparison.OrdinalIgnoreCase)))))throw new FormatException();
   if(svg.Attribute("viewBox") is null&&int.TryParse(svg.Attribute("width")?.Value,out var width)&&int.TryParse(svg.Attribute("height")?.Value,out var height)&&width>0&&height>0)svg.SetAttributeValue("viewBox",$"0 0 {width} {height}");
   return "<!doctype html><html><head><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; script-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>body{margin:0;background:white;display:flex;justify-content:center}svg{width:100%;height:256px;max-width:256px}</style></head><body>"+svg.ToString(SaveOptions.DisableFormatting)+"</body></html>";
  }
  catch(Exception e) when(e is XmlException or FormatException or ArgumentException){throw new InvalidOperationException("The setup image could not be displayed. Use the manual setup key instead.");}
 }
}
