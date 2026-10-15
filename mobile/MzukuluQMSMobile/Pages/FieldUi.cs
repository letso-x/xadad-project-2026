using Microsoft.Maui.Controls.Shapes;
using MzukuluQMSMobile.ViewModels;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.Pages;
internal static class FieldUi
{
 public static readonly Color Navy=Color.FromArgb("#112540"),Accent=Color.FromArgb("#AA4030"),Background=Color.FromArgb("#F7F9FB"),Line=Color.FromArgb("#DCE2EA"),Muted=Color.FromArgb("#53667C");
 public static Label Text(string text,double size=14,bool bold=false)=>new(){Text=text,FontSize=size,TextColor=Navy,FontAttributes=bold?FontAttributes.Bold:FontAttributes.None,LineBreakMode=LineBreakMode.WordWrap};
 public static Label Category(string text)=>new(){Text=text.ToUpperInvariant(),FontSize=11,CharacterSpacing=1.5,TextColor=Accent};
 public static Border Card(params View[] children)=>new(){BackgroundColor=Colors.White,Stroke=Line,StrokeThickness=1,StrokeShape=new RoundRectangle{CornerRadius=10},Padding=16,Content=new VerticalStackLayout{Spacing=10,Children={}}.With(children)};
 private static VerticalStackLayout With(this VerticalStackLayout layout,IEnumerable<View> children){foreach(var child in children)layout.Add(child);return layout;}
 public static View Section(string title)=>new Border{BackgroundColor=Navy,StrokeThickness=0,Padding=new Thickness(14,10),Content=new Label{Text=title.ToUpperInvariant(),TextColor=Colors.White,FontSize=12,FontAttributes=FontAttributes.Bold}};
 public static View Badge(string status)
 {
  var success=status is "Active" or "Approved" or "Completed" or "Closed" or "Synced" or "Current signature";
  var error=status is "Open" or "Rejected" or "Failed" or "Sync failed" or "Conflict";
  var returned=status=="ReturnedForCorrection";
  return new Border{StrokeThickness=returned?1:0,Stroke=Accent,BackgroundColor=Color.FromArgb(success?"#DCEFE3":error?"#FBE8E2":returned?"#FFF0DF":"#FCECCF"),Padding=new Thickness(9,5),StrokeShape=new RoundRectangle{CornerRadius=12},HorizontalOptions=LayoutOptions.Start,Content=new Label{Text=BusinessText.Status(status),FontSize=11,TextColor=Color.FromArgb(success?"#176247":error||returned?"#9B3024":"#855A10")}};
 }
 public static Button Action(string text,Func<Task> action,bool secondary=false)
 {
  var button=new Button{Text=text,BackgroundColor=secondary?Colors.White:Navy,TextColor=secondary?Navy:Colors.White,BorderColor=Line,BorderWidth=secondary?1:0,CornerRadius=7,MinimumHeightRequest=48,Padding=new Thickness(12,8)};
  SemanticProperties.SetDescription(button,text);
  button.Clicked+=async(_,_)=>{if(!button.IsEnabled)return;button.IsEnabled=false;try{await action();}finally{button.IsEnabled=true;}};return button;
 }
 public static View Warning(string text)=>new Border{Stroke=Accent,BackgroundColor=Color.FromArgb("#FCF0EC"),Padding=14,Content=Text(text)};
 public static Entry Entry(string placeholder,Keyboard? keyboard=null)=>new(){Placeholder=placeholder,Keyboard=keyboard??Keyboard.Default,TextColor=Navy,BackgroundColor=Colors.White,PlaceholderColor=Muted,MinimumHeightRequest=48};
}
// A keyboard-accessible, in-page dropdown with explicit contrast on Windows and mobile.
internal sealed class FieldChoice : VerticalStackLayout
{
 public string? SelectedValue {get;private set;}
 public event EventHandler? SelectionChanged;
 public FieldChoice(string title,IReadOnlyList<string> options,string? selected=null)
  :this(title,options.Select(o=>(Value:o,Label:o)).ToArray(),selected){}
 public FieldChoice(string title,IReadOnlyList<(string Value,string Label)> options,string? selected=null)
 {
  Spacing=6;SelectedValue=selected;var list=new VerticalStackLayout{Spacing=6,IsVisible=false};
  Button? toggle=null;
  string? SelectedLabel()=>options.FirstOrDefault(o=>o.Value==SelectedValue).Label;
  string Caption()=>(SelectedLabel()??title)+(list.IsVisible?"  \u25B4":"  \u25BE");
  toggle=FieldUi.Action(Caption(),()=>{list.IsVisible=!list.IsVisible;toggle!.Text=Caption();return Task.CompletedTask;},true);
  SemanticProperties.SetDescription(toggle,title+" dropdown. "+(SelectedLabel()??"No selection."));
  foreach(var option in options)list.Add(FieldUi.Action(option.Label,()=>{
   SelectedValue=option.Value;list.IsVisible=false;toggle.Text=Caption();SemanticProperties.SetDescription(toggle,title+". Selected: "+option.Label);
   SelectionChanged?.Invoke(this,EventArgs.Empty);return Task.CompletedTask;
  },true));
  Add(toggle);Add(list);
 }
}
public abstract class FieldScreen : ContentPage
{
 protected readonly VerticalStackLayout Rows=new(){Padding=new Thickness(18),Spacing=12};
 protected readonly Label Status=FieldUi.Text("");
 protected readonly ScrollView Scroller=new();
 protected FieldScreen(string title,FieldViewModel model,bool showMenu=true)
 {
  Title=title;BackgroundColor=FieldUi.Background;BindingContext=model;
  // Nested NavigationPage screens replace MAUI's automatic flyout icon with Back.
  // Keep navigation on the left, with record actions in the right-hand toolbar.
  var menu=new Button{Text="\u2630",AutomationId="NavigationMenu",FontSize=24,WidthRequest=48,HeightRequest=48,
   Padding=0,BackgroundColor=Colors.Transparent,TextColor=Colors.White,BorderWidth=0};
  menu.IsVisible=showMenu;
  SemanticProperties.SetDescription(menu,"Open navigation menu");
  menu.Clicked+=(_,_)=>{
   if(Window?.Page is FlyoutPage flyout)flyout.IsPresented=!flyout.IsPresented;
  };
  NavigationPage.SetTitleView(this,new HorizontalStackLayout{Spacing=8,VerticalOptions=LayoutOptions.Center,
   Children={menu,new Label{Text=title,FontSize=18,FontAttributes=FontAttributes.Bold,TextColor=Colors.White,
    VerticalOptions=LayoutOptions.Center,LineBreakMode=LineBreakMode.TailTruncation}}});
  Status.SetBinding(Label.TextProperty,nameof(FieldViewModel.Message));
  Scroller.Content=Rows;Content=Scroller;
 }
 protected void Heading(string category,string title){Rows.Add(FieldUi.Category(category));Rows.Add(FieldUi.Text(title,25,true));Rows.Add(Status);}
}
