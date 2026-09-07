using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HazzKaraokeHoster.App;
class Check {
[STAThread] static void Main() {
var app=new Application(); File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"broken-test.json"), System.Text.Json.JsonSerializer.Serialize(new Dictionary<string,string>{{Path.Combine(AppContext.BaseDirectory,"bad.mp3"),"test decoder failure"}})); var reg=typeof(AudienceWindow).Assembly.GetType("HazzKaraokeHoster.App.BrokenMediaRegistry")!; ((Task)reg.GetMethod("InitializeAsync")!.Invoke(null,new object[]{Path.Combine(AppContext.BaseDirectory,"broken-test.json")})!).GetAwaiter().GetResult(); var list=new ListBox();var w=new Window{Content=list,Opacity=0,ShowActivated=false,ShowInTaskbar=false};
w.Loaded+=async(_,_)=>{try{
var t=typeof(AudienceWindow).Assembly.GetType("HazzKaraokeHoster.App.BrokenMediaRegistry")!;
var store=Path.Combine(AppContext.BaseDirectory,"broken-test.json");
var path=Path.Combine(AppContext.BaseDirectory,"bad.mp3");

var bad=new {FilePath=path};list.Items.Add(bad);list.Items.Add(new {FilePath=path+"good"});w.UpdateLayout();
await Task.Delay(100);
var restored=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
if(restored.ToolTip?.ToString()?.StartsWith("BROKEN")!=true)throw new Exception("Saved tag not restored on restart");
t.GetMethod("Mark")!.Invoke(null,new object[]{path,"test decoder failure"});await Task.Delay(700);
var row=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
if(row.ToolTip?.ToString()?.StartsWith("BROKEN")!=true)throw new Exception("Missing broken label: context="+row.DataContext+" content="+row.Content+" loaded="+row.IsLoaded+" rows="+((System.Collections.Generic.ICollection<Control>)t.GetField("Rows",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Count);
if((row.Background as SolidColorBrush)?.Color.R!=135)throw new Exception("Missing broken color");
if(!File.ReadAllText(store).Contains("test decoder failure"))throw new Exception("Tag not persisted");
row.DataContext=new {FilePath=path+"good"};
if(row.ToolTip?.ToString()?.StartsWith("BROKEN")==true)throw new Exception("Recycled row retained broken tag");
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"result.txt"),"PASS: playback tag persisted; broken row colored and labeled; recycled healthy row cleared.");
}catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"result.txt"),ex.ToString());}finally{w.Close();app.Shutdown();}};app.Run(w);
}}



