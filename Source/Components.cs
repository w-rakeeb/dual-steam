using System;
using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DualSteam.UI {
public static class Ui {
 public static TextBlock Text(string value,double size=Theme.BodySize,string token="text.primary",bool bold=false){var t=new TextBlock{Text=value,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};t.SetResourceReference(TextBlock.FontSizeProperty,"type."+size);t.SetResourceReference(TextBlock.LineHeightProperty,"line."+size);if(value.Length>100){t.MaxWidth=Theme.ProseMax;t.HorizontalAlignment=HorizontalAlignment.Left;}Theme.Bind(t,TextBlock.ForegroundProperty,token);return t;}
 public static Button Button(string title,bool primary=false,Action action=null){var b=new Button{Content=title,Style=Theme.Style(primary?"Primary":"Button"),HorizontalContentAlignment=HorizontalAlignment.Center,TabIndex=10};Motion(b,primary);AutomationProperties.SetName(b,title);if(action!=null)b.Click+=delegate{action();};return b;}
 public static void Motion(Button button,bool primary=false){
  Action<string> animate=token=>{if(!button.IsEnabled||!Theme.Motion)return;button.ApplyTemplate();var plate=button.Template.FindName("Plate",button) as Border;if(plate==null)return;var current=plate.Background as SolidColorBrush;var target=(token==null?button.Background:Theme.B(token)) as SolidColorBrush;if(current==null||target==null)return;var brush=current.Clone();plate.Background=brush;var animation=new ColorAnimation(target.Color,TimeSpan.FromMilliseconds(Theme.HoverMs)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}};animation.Completed+=delegate{plate.ClearValue(Border.BackgroundProperty);};brush.BeginAnimation(SolidColorBrush.ColorProperty,animation);};
  button.MouseEnter+=delegate{animate(primary?"accent.hover":"bg.surfaceHover");};button.MouseLeave+=delegate{animate(null);};button.PreviewMouseLeftButtonDown+=delegate{animate(primary?"accent.pressed":"bg.surfaceRaised");};button.PreviewMouseLeftButtonUp+=delegate{animate(primary?"accent.hover":"bg.surfaceHover");};button.IsEnabledChanged+=delegate{if(!button.IsEnabled&&button.Template!=null){var plate=button.Template.FindName("Plate",button) as Border;if(plate!=null)plate.ClearValue(Border.BackgroundProperty);}};
 }
 public static Button IconButton(string glyph,string label,Action action){var b=Button(glyph,false,action);b.Style=Theme.Style("Quiet");b.MinWidth=Theme.Hit;b.Width=Theme.Hit;b.Height=Theme.Hit;b.Padding=new Thickness(0);b.FontFamily=(FontFamily)Theme.Resources["Font.Icons"];b.ToolTip=label;AutomationProperties.SetName(b,label);return b;}
 public static TextBlock Glyph(string glyph,double size=Theme.TitleSize){var t=Text(glyph,size);t.FontSize=size;t.LineHeight=size;t.FontFamily=(FontFamily)Theme.Resources["Font.Icons"];return t;}
 public static void Name(DependencyObject target,string name){AutomationProperties.SetName(target,name);}
 public static Border Divider(){var b=new Border{Height=1,Margin=new Thickness(Theme.CardPadding,0,Theme.CardPadding,0)};Theme.Bind(b,Border.BackgroundProperty,"border.subtle");return b;}
}
public class Card:Border {
 public Card(){CornerRadius=new CornerRadius(Theme.CardRadius);Padding=new Thickness(Theme.CardPadding);BorderThickness=new Thickness(1);Theme.Bind(this,BackgroundProperty,"bg.surface");Theme.Bind(this,BorderBrushProperty,"border.subtle");}
}
public class StatusPill:Border {
 readonly TextBlock label;public StatusPill(){CornerRadius=new CornerRadius(Theme.StatusRadius);BorderThickness=new Thickness(1);Theme.Bind(this,BorderBrushProperty,"border.subtle");Padding=new Thickness(8,0,8,0);MinHeight=24;VerticalAlignment=VerticalAlignment.Center;label=Ui.Text("●  Closed",Theme.NoteSize);label.VerticalAlignment=VerticalAlignment.Center;Child=label;Set("Closed","neutral","");}
 public void Set(string text,string state,string name){label.Text="●  "+text;Theme.Bind(label,TextBlock.ForegroundProperty,"status."+state+"Text");Theme.Bind(this,BackgroundProperty,"status."+state+"Bg");Ui.Name(label,name+", "+text.ToLowerInvariant());AutomationProperties.SetLiveSetting(label,AutomationLiveSetting.Polite);}
}
public class ProfileCard:Card {
 public readonly Button Open,Quit;public readonly StatusPill Pill=new StatusPill();public readonly TextBlock Usage,Subtitle;
 readonly Border iconTile;public readonly string NameLabel;public bool Running,Busy;
 public ProfileCard(string name,string description,string glyph){
  NameLabel=name;var grid=new Grid();foreach(var width in new[]{new GridLength(52),new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=width});
  var tile=new Border{Width=40,Height=40,CornerRadius=new CornerRadius(10),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};Theme.Bind(tile,BackgroundProperty,"bg.surfaceRaised");var icon=Ui.Glyph(glyph,20);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.VerticalAlignment=VerticalAlignment.Center;tile.Child=icon;iconTile=tile;grid.Children.Add(tile);
  var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,Theme.Gap,0)};copy.Children.Add(Ui.Text(name,Theme.TitleSize,"text.primary",true));Subtitle=Ui.Text(description,Theme.NoteSize,"text.secondary");Subtitle.TextWrapping=TextWrapping.NoWrap;Subtitle.TextTrimming=TextTrimming.CharacterEllipsis;Subtitle.ToolTip=description;copy.Children.Add(Subtitle);Usage=Ui.Text("",Theme.NoteSize,"text.muted");Usage.Visibility=Visibility.Collapsed;copy.Children.Add(Usage);Grid.SetColumn(copy,1);grid.Children.Add(copy);
  Pill.MinWidth=Theme.StatusWidth;Pill.HorizontalAlignment=HorizontalAlignment.Right;Pill.Margin=new Thickness(0,0,Theme.Gap,0);Grid.SetColumn(Pill,2);grid.Children.Add(Pill);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Quit=Ui.Button("Quit");Open=Ui.Button("Open",true);Quit.MinWidth=Theme.ActionWidth;Open.MinWidth=Theme.ActionWidth;Quit.Margin=new Thickness(0,0,8,0);actions.Children.Add(Quit);actions.Children.Add(Open);Grid.SetColumn(actions,3);grid.Children.Add(actions);Child=grid;
  Ui.Name(Open,"Open "+name);Ui.Name(Quit,"Quit "+name);MouseEnter+=delegate{Theme.Bind(this,BackgroundProperty,"bg.surfaceRaised");};MouseLeave+=delegate{Theme.Bind(this,BackgroundProperty,"bg.surface");};
 }
 public void SetLogo(string file){if(!File.Exists(file))return;var source=new BitmapImage();source.BeginInit();source.CacheOption=BitmapCacheOption.OnLoad;source.UriSource=new Uri(file,UriKind.Absolute);source.EndInit();source.Freeze();iconTile.Child=new Image{Source=source,Width=28,Height=28,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};}
 public void Update(System.Diagnostics.Process[] processes,bool created=true){Running=processes.Length>0;Pill.Set(Running?"Running":"Closed",Running?"success":"neutral",NameLabel);Usage.Text=Running?"PID "+String.Join(", ",processes.Select(p=>p.Id.ToString())):"Not running";Usage.Visibility=Visibility.Visible;if(!Busy){Quit.IsEnabled=Running;Open.Content=Running?"Focus":created?"Open":"Set up";}Ui.Name(Open,(Running?"Focus ":"Open ")+NameLabel);}
 public void Loading(bool opening){Busy=true;Open.IsEnabled=false;Quit.IsEnabled=false;(opening?Open:Quit).Content=opening?"Starting…":"Quitting…";Pill.Set(opening?"Starting":"Quitting","warning",NameLabel);}
 public void EndLoading(){Busy=false;Open.IsEnabled=true;Quit.Content="Quit";}
}
public class ToggleSwitch:CheckBox {
 public ToggleSwitch(){Style=Theme.Style("Toggle");TabIndex=10;VerticalAlignment=VerticalAlignment.Center;Checked+=delegate{Move();};Unchecked+=delegate{Move();};}
 public override void OnApplyTemplate(){base.OnApplyTemplate();Move(false);}
 void Move(bool animate=true){var transform=Template.FindName("Thumb",this) as TranslateTransform;if(transform==null)return;double to=IsChecked==true?20:0;if(animate&&Theme.Motion)transform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(to,TimeSpan.FromMilliseconds(Theme.HoverMs)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}});else{transform.BeginAnimation(TranslateTransform.XProperty,null);transform.X=to;}}
}
public class SettingRow:Grid {
 public readonly ToggleSwitch Toggle=new ToggleSwitch();public readonly TextBlock Lock;
 public SettingRow(string title,string description){MinHeight=Theme.RowHeight;Margin=new Thickness(Theme.CardPadding,0,Theme.CardPadding,0);ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,8,Theme.Gap,8)};copy.Children.Add(Ui.Text(title,Theme.BodySize,"text.primary",true));var detail=Ui.Text(description,Theme.NoteSize,"text.secondary");detail.Margin=new Thickness(0,2,0,0);copy.Children.Add(detail);Children.Add(copy);Lock=Ui.Glyph("\uE72E");Lock.VerticalAlignment=VerticalAlignment.Center;Lock.Margin=new Thickness(0,0,8,0);Lock.Visibility=Visibility.Collapsed;Grid.SetColumn(Lock,1);Children.Add(Lock);Grid.SetColumn(Toggle,2);Children.Add(Toggle);Ui.Name(Toggle,title);Background=Brushes.Transparent;Cursor=Cursors.Hand;MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(!IsChildOf(e.OriginalSource as DependencyObject,Toggle)&&Toggle.IsEnabled){Toggle.IsChecked=Toggle.IsChecked!=true;Toggle.Focus();e.Handled=true;}};}
 static bool IsChildOf(DependencyObject item,DependencyObject ancestor){while(item!=null){if(item==ancestor)return true;item=VisualTreeHelper.GetParent(item);}return false;}
 public void Locked(bool value,string reason=null){Toggle.IsEnabled=!value;Lock.Visibility=value?Visibility.Visible:Visibility.Collapsed;Cursor=value?Cursors.No:Cursors.Hand;ToolTip=value?(reason??"Quit the second profile to change this setting."):null;}
}
public class InfoBanner:Border {
 readonly TextBlock text;public InfoBanner(string message,bool error=false){CornerRadius=new CornerRadius(Theme.ButtonRadius);Padding=new Thickness(Theme.Gap);var panel=new Grid();panel.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});panel.ColumnDefinitions.Add(new ColumnDefinition());var icon=Ui.Glyph(error?"\uE783":"\uE946");icon.Margin=new Thickness(0,0,8,0);Theme.Bind(icon,TextBlock.ForegroundProperty,error?"status.dangerText":"accent.text");panel.Children.Add(icon);text=Ui.Text(message,Theme.NoteSize,error?"status.dangerText":"text.primary");Grid.SetColumn(text,1);panel.Children.Add(text);Child=panel;Theme.Bind(this,BackgroundProperty,error?"status.dangerBg":"accent.subtle");AutomationProperties.SetLiveSetting(this,AutomationLiveSetting.Polite);}
 public void Message(string value){text.Text=value;}
}
public class NavItem:Button {
 readonly Border bar;readonly TextBlock label;public readonly string Page;
 public NavItem(string page,string glyph){Page=page;Style=Theme.Style(page=="About"?"FooterNav":"Quiet");MinWidth=0;MinHeight=40;Height=double.NaN;Margin=new Thickness(8,2,8,2);Padding=new Thickness(0);HorizontalContentAlignment=HorizontalAlignment.Stretch;var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(4)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(36)});g.ColumnDefinitions.Add(new ColumnDefinition());bar=new Border{Width=3,Height=20,CornerRadius=new CornerRadius(2),Visibility=Visibility.Hidden};Theme.Bind(bar,Border.BackgroundProperty,"accent.default");g.Children.Add(bar);var icon=Ui.Glyph(glyph,16);icon.VerticalAlignment=VerticalAlignment.Center;icon.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(icon,1);g.Children.Add(icon);label=Ui.Text(page);label.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(label,2);g.Children.Add(label);Content=g;Ui.Name(this,page);ToolTip=page;}
 public void Active(bool value){bar.Visibility=value?Visibility.Visible:Visibility.Hidden;if(Page=="About")Background=Brushes.Transparent;else Theme.Bind(this,BackgroundProperty,value?"accent.subtle":"bg.sidebar");AutomationProperties.SetItemStatus(this,value?"Current page":"");}
 public void Compact(bool value){label.Visibility=value?Visibility.Collapsed:Visibility.Visible;ToolTip=value?Page:null;}
}
public class Toast:Border {
 readonly TextBlock label;readonly System.Windows.Threading.DispatcherTimer timer=new System.Windows.Threading.DispatcherTimer();
 public Toast(){Visibility=Visibility.Collapsed;HorizontalAlignment=HorizontalAlignment.Right;VerticalAlignment=VerticalAlignment.Bottom;MaxWidth=400;Margin=new Thickness(Theme.Padding);Padding=new Thickness(Theme.CardPadding);CornerRadius=new CornerRadius(Theme.CardRadius);Theme.Bind(this,BackgroundProperty,"bg.surfaceRaised");label=Ui.Text("");Child=label;AutomationProperties.SetLiveSetting(this,AutomationLiveSetting.Polite);timer.Interval=TimeSpan.FromMilliseconds(Theme.ToastMs);timer.Tick+=delegate{timer.Stop();Visibility=Visibility.Collapsed;};}
 public void Show(string value){label.Text=value;Visibility=Visibility.Visible;timer.Stop();timer.Start();if(Theme.Motion){Opacity=0;BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(180)));var slide=new TranslateTransform();RenderTransform=slide;slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(8,0,TimeSpan.FromMilliseconds(180)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}});}Ui.Name(label,value);}
 public void Dismiss(){timer.Stop();Visibility=Visibility.Collapsed;}
}
}

