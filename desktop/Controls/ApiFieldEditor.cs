using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MzuApplication.Data;
using MzuApplication.Models;
namespace MzuApplication.Controls
{
 internal static class ApiFieldEditor
 {
  public static Control Create(object field,object value,Action<object> changed)
  {
   var type=Json.Text(field,"fieldType");
   if(type=="Dropdown" && (Json.Get(field,"options") as object[]??new object[0]).Select(Convert.ToString).OrderBy(s=>s).SequenceEqual(new[]{"A","N/A","R"}))
   {
    var selector=new ResponseSelector();selector.Value=Convert.ToString(value)=="A"?ItemResponse.Accepted:Convert.ToString(value)=="R"?ItemResponse.Rejected:Convert.ToString(value)=="N/A"?ItemResponse.NotApplicable:ItemResponse.Unanswered;
    selector.ValueChanged+=(s,e)=>changed(selector.Value==ItemResponse.Accepted?"A":selector.Value==ItemResponse.Rejected?"R":selector.Value==ItemResponse.NotApplicable?"N/A":null);return selector;
   }
   if(type=="Dropdown" || type=="YesNo" || type=="Boolean")
   {
    var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=350};combo.Items.Add("");
    var options=type=="Dropdown"?(Json.Get(field,"options") as object[]??new object[0]):type=="Boolean"?new object[]{true,false}:new object[]{"Yes","No"};
    combo.Items.AddRange(options);combo.SelectedItem=value??"";combo.SelectedIndexChanged+=(s,e)=>changed(combo.SelectedIndex<=0?null:combo.SelectedItem);return combo;
   }
   if(type=="MultiSelect")
   {
    var list=new CheckedListBox{Width=420,Height=90};var selected=(value as object[]??new object[0]).Select(Convert.ToString).ToList();
    foreach(var option in Json.Get(field,"options") as object[]??new object[0])list.Items.Add(option,selected.Contains(Convert.ToString(option)));
    list.ItemCheck+=(s,e)=>{var values=new List<object>();for(int i=0;i<list.Items.Count;i++)if(i==e.Index?e.NewValue==CheckState.Checked:list.GetItemChecked(i))values.Add(list.Items[i]);changed(values.ToArray());};return list;
   }
   if(type=="Date" || type=="DateTime")
   {
    var date=new DateTimePicker{Width=300,ShowCheckBox=true,Format=DateTimePickerFormat.Custom,CustomFormat=type=="Date"?"yyyy-MM-dd":"yyyy-MM-dd HH:mm"};DateTime parsed;
    date.Checked=value!=null && DateTime.TryParse(Convert.ToString(value),out parsed);if(DateTime.TryParse(Convert.ToString(value),out parsed))date.Value=parsed;
    date.ValueChanged+=(s,e)=>changed(date.Checked?(object)date.Value.ToString(type=="Date"?"yyyy-MM-dd":"o",CultureInfo.InvariantCulture):null);return date;
   }
   var text=new TextInput{Width=Dpi.S(480),Value=Convert.ToString(value,CultureInfo.InvariantCulture)??"",Multiline=type=="LongText",Height=Dpi.S(type=="LongText"?80:36),Placeholder=Json.Text(field,"placeholder")};
   text.ValueChanged+=(s,e)=>
   {
    if(string.IsNullOrEmpty(text.Value)){changed(null);return;}
    decimal number;
    if(type=="Number"||type=="Decimal") { if(decimal.TryParse(text.Value,NumberStyles.Number,CultureInfo.InvariantCulture,out number))changed(number);else changed(text.Value); }
    else changed(text.Value);
   };
   return text;
  }
 }
}
