using System;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using DualSteam.UI;
public static class AppDialog {
 public static MessageBoxResult Show(string message,string title="Dual Steam"){return Show(null,message,title);}
 public static MessageBoxResult Show(Window owner,string message,string title="Dual Steam",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None){
  if(Application.Current==null||Theme.Resources.Count==0)return MessageBox.Show(owner,message,title,buttons,image);
  var dialog=new Window{Title=title,Width=520,SizeToContent=SizeToContent.Height,MaxHeight=Math.Max(320,SystemParameters.WorkArea.Height-80),ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,ShowInTaskbar=false,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner};if(owner!=null)dialog.Owner=owner;
  Theme.Bind(dialog,Control.BackgroundProperty,"bg.surface");Theme.Bind(dialog,Control.FontFamilyProperty,"Font.App");
  var frame=new Border{Padding=new Thickness(24),BorderThickness=new Thickness(1)};Theme.Bind(frame,Border.BorderBrushProperty,"border.sidebar");var body=new StackPanel();frame.Child=body;dialog.Content=frame;
  var heading=Ui.Text(title,20,"text.primary",true);heading.Margin=new Thickness(0,0,0,16);body.Children.Add(heading);Ui.Name(dialog,title);dialog.MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource==heading)dialog.DragMove();};
  var text=Ui.Text(message,13,"text.secondary");text.MaxWidth=470;body.Children.Add(new ScrollViewer{Content=text,MaxHeight=Math.Max(120,Math.Min(360,SystemParameters.WorkArea.Height-260)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};body.Children.Add(actions);MessageBoxResult result=buttons==MessageBoxButton.YesNo?MessageBoxResult.No:buttons==MessageBoxButton.OK?MessageBoxResult.OK:MessageBoxResult.Cancel;
  if(image==MessageBoxImage.Warning||message.Length>400){var copy=Ui.Button("Copy details",false,()=>{try{Clipboard.SetText(title+Environment.NewLine+message);}catch{}});copy.Margin=new Thickness(0,0,12,0);actions.Children.Add(copy);}
  Action<string,MessageBoxResult,bool> add=(label,value,primary)=>{var button=Ui.Button(label,primary,()=>{result=value;dialog.Close();});button.MinWidth=80;button.Margin=new Thickness(8,0,0,0);button.IsDefault=buttons==MessageBoxButton.YesNo||buttons==MessageBoxButton.YesNoCancel?value==MessageBoxResult.No:primary;button.IsCancel=value==MessageBoxResult.Cancel||value==MessageBoxResult.No||buttons==MessageBoxButton.OK;actions.Children.Add(button);};
  if(buttons==MessageBoxButton.YesNo||buttons==MessageBoxButton.YesNoCancel){if(buttons==MessageBoxButton.YesNoCancel)add("Cancel",MessageBoxResult.Cancel,false);add("No",MessageBoxResult.No,false);add("Yes",MessageBoxResult.Yes,true);}else{if(buttons==MessageBoxButton.OKCancel)add("Cancel",MessageBoxResult.Cancel,false);add("OK",MessageBoxResult.OK,true);}
  dialog.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){dialog.Close();e.Handled=true;}};dialog.ShowDialog();return result;
 }
}
