using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Data;

namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private sealed record SingerListScope(string Name, Guid? Venue, bool Current);
    private async void SingerDatabaseManager_Click(object sender, RoutedEventArgs e)
    {
        if (_venueDialogOpen || _venueClosing) return;
        _venueDialogOpen = true;
        try { await ShowSingerDatabaseManagerAsync(); }
        catch (Exception ex) { App.WriteDiagnostic("SINGER MANAGER", ex.ToString()); MessageBox.Show(this, ex.Message, "Singer Database Manager"); }
        finally { _venueDialogOpen = false; }
    }
    private async Task ShowSingerDatabaseManagerAsync()
    {
        var profiles = File.Exists(VenueProfilesPath) ? JsonSerializer.Deserialize<List<VenueProfile>>(File.ReadAllText(VenueProfilesPath)) ?? new() : new List<VenueProfile>();
        var scopes = new List<SingerListScope> { new("Current saved singers" + (_activeSingerVenue is Guid a ? " — " + profiles.FirstOrDefault(p=>p.Id==a)?.Name : " — no venue"), _activeSingerVenue, true) };
        scopes.AddRange(profiles.Where(p=>p.Id!=_activeSingerVenue).OrderBy(p=>p.Name).Select(p=>new SingerListScope(p.Name,p.Id,false)));
        var folder=Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!,"venue-singers"); Directory.CreateDirectory(folder);
        string Snapshot(Guid id)=>Path.Combine(folder,id.ToString("N")+".db");
        string? ScopePath(SingerListScope scope)=>scope.Current ? _db.DatabasePath : profiles.Single(p=>p.Id==scope.Venue).SingerSnapshot is Guid id ? Snapshot(id) : null;
        var window=new Window { Owner=this,Title="Singer Database Manager",Width=Math.Min(1000,SystemParameters.WorkArea.Width-30),Height=Math.Min(720,SystemParameters.WorkArea.Height-30),MinWidth=640,MinHeight=430,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        var panel=new DockPanel { Margin=new Thickness(12) }; window.Content=panel;
        var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); panel.Children.Add(header);
        header.Children.Add(new TextBlock { Text="Saved singers and permanent history. Tonight’s rotation is separate: use REMOVE in the main singer list to remove someone from tonight only, keeping their saved name and history.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) });
        var scopeBox=new ComboBox { ItemsSource=scopes,DisplayMemberPath="Name",SelectedIndex=0,MinHeight=30 }; header.Children.Add(scopeBox);
        var searchRow=new DockPanel { Margin=new Thickness(0,6,0,6) }; header.Children.Add(searchRow);
        var searchButton=new Button { Content="SEARCH",MinWidth=90 }; DockPanel.SetDock(searchButton,Dock.Right); searchRow.Children.Add(searchButton);
        var search=new TextBox { MinHeight=30,ToolTip="Search saved singer names; press Enter" }; searchRow.Children.Add(search);
        var buttons=new WrapPanel(); header.Children.Add(buttons);
        var destination=new ComboBox { ItemsSource=scopes,DisplayMemberPath="Name",MinWidth=220,MinHeight=30,Margin=new Thickness(4),ToolTip="Destination list for Copy / Move" }; header.Children.Add(new TextBlock { Text="Copy / move destination:" }); header.Children.Add(destination);
        var status=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,8) }; header.Children.Add(status);
        var grid=new DataGrid { AutoGenerateColumns=true,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Extended,EnableRowVirtualization=true,EnableColumnVirtualization=true }; panel.Children.Add(grid);
        var busy=false;
        window.Closing+=(_,args)=> { if(busy) args.Cancel=true; };
        async Task Run(Func<Task> action)
        {
            if(busy)return; busy=true; header.IsEnabled=false; grid.IsEnabled=false;
            try { await action(); }
            catch(Exception ex) { status.Text=ex.Message; App.WriteDiagnostic("SINGER MANAGER ACTION",ex.ToString()); }
            finally { busy=false; header.IsEnabled=true; grid.IsEnabled=true; }
        }
        SingerListScope Scope()=>(SingerListScope)scopeBox.SelectedItem;
        async Task Refresh()
        {
            var path=ScopePath(Scope()); var rows=path is null ? new List<SavedSingerRow>() : await new SingerDatabaseManager(path).SearchAsync(search.Text);
            grid.ItemsSource=rows; status.Text=$"{rows.Count:N0} matching saved singers. Ctrl/Shift selects several. Changes affect only the selected list.";
        }
        long[] Selected()=>grid.SelectedItems.Cast<SavedSingerRow>().Select(s=>s.Id).ToArray();
        void Guard(long[] ids,bool removesNames)
        {
            if(ids.Length==0)throw new InvalidOperationException("Select at least one saved singer.");
            if(_karaokePlaying || _karaokePaused || _karaokePresentationActive)throw new InvalidOperationException("Stop karaoke before editing saved singers.");
            if(Scope().Current && removesNames && _queue.Any(s=>s.SingerId is long id && ids.Contains(id)))throw new InvalidOperationException("A selected singer is in tonight’s rotation. Remove them from tonight using the main singer list first; their history will be kept until you confirm deletion here.");
        }
        bool Confirm(string text)=>MessageBox.Show(window,text,"Confirm saved-singer change",MessageBoxButton.YesNo,MessageBoxImage.None)==MessageBoxResult.Yes;
        void PersistProfiles()
        {
            var temp=VenueProfilesPath+".manager.tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(profiles,new JsonSerializerOptions { WriteIndented=true })); File.Move(temp,VenueProfilesPath,true);
        }
        async Task Edit(SingerListScope scope,Func<SingerDatabaseManager,Task> action,Action<VenueProfile>? rosterChange=null)
        {
            if(scope.Current)
            {
                var backup=Path.Combine(folder,"backups",Guid.NewGuid().ToString("N")+".db");
                await new VenueSingerStore(_db).SaveAsync(backup);
                File.WriteAllText(backup+".queue.json",JsonSerializer.Serialize(CaptureVenueRoster()));
                await action(new SingerDatabaseManager(_db.DatabasePath));
                await RefreshSavedSingerNamesAsync();
                // Explicit user edits are saved immediately; no background/timed saving is added.
                await SaveActiveVenueAsync();
                profiles=File.Exists(VenueProfilesPath)?JsonSerializer.Deserialize<List<VenueProfile>>(File.ReadAllText(VenueProfilesPath))??new():new();
            }
            else
            {
                var profile=profiles.Single(p=>p.Id==scope.Venue); var id=Guid.NewGuid(); var staged=Snapshot(id); var original=ScopePath(scope);
                if(original is not null)
                {
                    File.Copy(original,staged);
                    var backup=Path.Combine(folder,"backups",Guid.NewGuid().ToString("N")+".db"); Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(original,backup); File.WriteAllText(backup+".queue.json",JsonSerializer.Serialize(profile.SingerRoster));
                }
                else
                {
                    await new VenueSingerStore(_db).SaveAsync(staged);
                    var empty=new SingerDatabaseManager(staged); await empty.DeleteAsync((await empty.SearchAsync("")).Select(s=>s.Id).ToArray(),false);
                }
                await action(new SingerDatabaseManager(staged));
                var prior=JsonSerializer.Serialize(profile);
                try { profile.SingerSnapshot=id; rosterChange?.Invoke(profile); PersistProfiles(); }
                catch { profiles[profiles.IndexOf(profile)]=JsonSerializer.Deserialize<VenueProfile>(prior)!; throw; }
                // Keep the previous singer-only snapshot available for recovery.
            }
        }
        void Button(string label,Func<Task> action)
        {
            var b=new Button { Content=label,Padding=new Thickness(8,5,8,5),Margin=new Thickness(3) }; buttons.Children.Add(b); b.Click+=async(_,_)=>await Run(action);
        }
        Button("VIEW / DELETE HISTORY",async()=>
        {
            var ids=Selected(); Guard(ids,false); if(ids.Length!=1)throw new InvalidOperationException("Select one singer to view individual history entries.");
            var scope=Scope(); var path=ScopePath(scope)!;
            var historyWindow=new Window { Owner=window,Title="History — select entries to delete",Width=800,Height=500,WindowStartupLocation=WindowStartupLocation.CenterOwner };
            var dock=new DockPanel { Margin=new Thickness(12) }; historyWindow.Content=dock;
            var del=new Button { Content="DELETE SELECTED HISTORY ENTRIES",MinHeight=34 }; DockPanel.SetDock(del,Dock.Bottom); dock.Children.Add(del);
            var historyGrid=new DataGrid { IsReadOnly=true,AutoGenerateColumns=true,SelectionMode=DataGridSelectionMode.Extended,ItemsSource=await new SingerDatabaseManager(path).HistoryAsync(ids[0]) }; dock.Children.Add(historyGrid);
            long[] chosen=[]; del.Click+=(_,_)=> { chosen=historyGrid.SelectedItems.Cast<SavedHistoryRow>().Select(h=>h.Id).ToArray(); if(chosen.Length>0)historyWindow.DialogResult=true; };
            if(historyWindow.ShowDialog()==true && Confirm($"Delete {chosen.Length} history entries? Keep the singer name and tonight’s rotation.")) { await Edit(scope,s=>s.DeleteAsync(chosen,true,true)); await Refresh(); }
        });
        Button("CLEAR SELECTED SINGERS’ HISTORY",async()=>
        {
            var ids=Selected(); Guard(ids,false);
            if(!Confirm($"Permanently clear history for {ids.Length} selected singers? Keep their names and tonight’s rotation."))return;
            await Edit(Scope(),s=>s.DeleteAsync(ids,true)); await Refresh();
        });
        Button("DELETE SAVED SINGERS",async()=>
        {
            var ids=Selected(); Guard(ids,true);
            if(!Confirm($"Delete {ids.Length} saved singer names AND their permanent history from this list? This is different from removing someone from tonight’s rotation."))return;
            await Edit(Scope(),s=>s.DeleteAsync(ids,false),p=>p.SingerRoster.RemoveAll(s=>s.SingerId is long id && ids.Contains(id))); await Refresh();
        });
        Button("MERGE DUPLICATES",async()=>
        {
            var selected=grid.SelectedItems.Cast<SavedSingerRow>().ToArray(); var ids=Selected(); Guard(ids,true);
            if(ids.Length<2)throw new InvalidOperationException("Select two or more duplicate names.");
            var choose=new Window { Owner=window,Title="Choose the saved singer name to keep",Width=450,Height=320,WindowStartupLocation=WindowStartupLocation.CenterOwner };
            var dock=new DockPanel { Margin=new Thickness(12) }; choose.Content=dock; var accept=new Button { Content="KEEP THIS NAME AND COMBINE HISTORY",MinHeight=34 }; DockPanel.SetDock(accept,Dock.Bottom); dock.Children.Add(accept);
            var names=new ListBox { ItemsSource=selected,DisplayMemberPath="Name",SelectedIndex=0 }; dock.Children.Add(names); accept.Click+=(_,_)=>choose.DialogResult=true;
            if(choose.ShowDialog()!=true)return; var keep=(SavedSingerRow)names.SelectedItem;
            if(!Confirm($"Keep '{keep.Name}' and combine all {ids.Length} singers’ history and notes? Other selected saved names will be removed."))return;
            await Edit(Scope(),s=>s.MergeAsync(keep.Id,ids),p=>
            {
                var rows=p.SingerRoster.Where(s=>s.SingerId is long id && ids.Contains(id)).ToList();
                if(rows.Count>0)
                {
                    var first=rows[0]; first.SingerId=keep.Id; first.SingerName=keep.Name;
                    foreach(var other in rows.Skip(1)) { first.Songs.AddRange(other.Songs.Where(song=>first.Songs.All(existing=>existing.QueueSongId!=song.QueueSongId))); p.SingerRoster.Remove(other); }
                }
            });
            var venue=Scope().Venue?.ToString()??"default";
            foreach(var other in selected.Where(s=>s.Id!=keep.Id)) CopyPhoto(venue,other.Name,venue,keep.Name);
            await Refresh();
        });
        async Task Transfer(bool move)
        {
            var selected=grid.SelectedItems.Cast<SavedSingerRow>().ToArray(); var ids=Selected(); Guard(ids,move); var from=Scope();
            if(destination.SelectedItem is not SingerListScope to || from==to)throw new InvalidOperationException("Choose a different destination list.");
            if(!Confirm($"{(move?"Move":"Copy")} {ids.Length} singers and their history from '{from.Name}' to '{to.Name}'? Matching names in the destination are combined. {(move?"Source saved names/history will be removed after the destination is saved.":"Source stays unchanged.")} Tonight’s rotation is not copied."))return;
            var source=ScopePath(from)!;
            await Edit(to,s=>s.CopyFromAsync(source,ids));
            foreach(var row in selected)CopyPhoto(from.Venue?.ToString()??"default",row.Name,to.Venue?.ToString()??"default",row.Name);
            if(move)await Edit(from,s=>s.DeleteAsync(ids,false),p=>p.SingerRoster.RemoveAll(s=>s.SingerId is long id && ids.Contains(id)));
            await Refresh(); status.Text=move?"Moved selected saved singers and history. Destination was saved before source removal.":"Copied selected saved singers and history. Source unchanged.";
        }
        Button("COPY TO LIST",()=>Transfer(false)); Button("MOVE TO LIST",()=>Transfer(true));
        searchButton.Click+=async(_,_)=>await Run(Refresh);
        search.KeyDown+=async(_,args)=> { if(args.Key==System.Windows.Input.Key.Enter) { args.Handled=true; await Run(Refresh); } };
        scopeBox.SelectionChanged+=async(_,_)=>await Run(Refresh);
        await Run(Refresh); window.ShowDialog();
    }
    private void CopyPhoto(string sourceVenue,string sourceName,string targetVenue,string targetName)
    {
        try
        {
            var source=SingerPortrait.PhotoPath(sourceVenue,sourceName); var target=SingerPortrait.PhotoPath(targetVenue,targetName);
            if(File.Exists(source) && !File.Exists(target)) { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source,target); }
            SingerPortrait.InvalidatePhotoCache(); SingerPhotoRevision++;
        }
        catch(Exception ex) { App.WriteDiagnostic("SINGER MANAGER PHOTO",ex.ToString()); MessageBox.Show(this,"Singer data was saved, but the photo could not be copied: "+ex.Message,"Singer photo"); }
    }
}
