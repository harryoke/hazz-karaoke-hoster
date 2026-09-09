$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../src/HazzKaraokeHoster.App/MainWindow.xaml.cs') -Raw
$start = $source.IndexOf('    private async void MainWindow_Closing(')
$end = $source.IndexOf('    private async Task RefreshLibraryAutoWatchAsync()', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Cannot locate production close handler' }
$handler = $source.Substring($start, $end - $start).Replace('MessageBox.Show(', 'TestMessageBox.Show(')
$generated = "using System.ComponentModel; using System.Windows; namespace ShutdownChecks; partial class MainWindow {`n" + $handler + "`n}"
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'ProductionHandler.cs') -Value $generated
