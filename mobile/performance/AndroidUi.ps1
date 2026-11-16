param(
 [ValidateSet('Read','Tap','ScrollTo')][string]$Action='Read',
 [string]$Text='',
 [int]$Occurrence=1,
 [string]$Device='emulator-5554'
)
$ErrorActionPreference='Stop'
$adb='C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe'
function Read-DeviceUi {
 & $adb -s $Device shell uiautomator dump /sdcard/qms-perf-view.xml | Out-Null
 [xml]$tree=((& $adb -s $Device shell cat /sdcard/qms-perf-view.xml) -join '')
 return ,$tree
}
function Find-Buttons($tree) {
 @($tree.SelectNodes('//node') | Where-Object {$_.text -ceq $Text -and $_.clickable -eq 'true' -and $_.enabled -eq 'true'})
}
$tree=Read-DeviceUi
if($Action -eq 'ScrollTo') {
 for($step=0;$step -lt 24;$step++) {
  $buttons=@(Find-Buttons $tree)
  if($buttons.Count -ge $Occurrence){break}
  & $adb -s $Device shell input swipe 540 2150 540 850 800
  $next=Read-DeviceUi
  if($next.OuterXml -eq $tree.OuterXml){throw "Reached end without finding '$Text'."}
  $tree=$next
 }
 if(@(Find-Buttons $tree).Count -lt $Occurrence){throw "Scroll limit reached without finding '$Text'."}
}
if($Action -eq 'Tap') {
 $buttons=@(Find-Buttons $tree)
 if($Occurrence -lt 1 -or $buttons.Count -lt $Occurrence){throw "No observed clickable '$Text' at occurrence $Occurrence."}
 $bounds=[regex]::Match($buttons[$Occurrence-1].bounds,'\[(\d+),(\d+)\]\[(\d+),(\d+)\]')
 if(-not $bounds.Success){throw 'Invalid observed bounds.'}
 & $adb -s $Device shell input tap (([int]$bounds.Groups[1].Value+[int]$bounds.Groups[3].Value)/2) (([int]$bounds.Groups[2].Value+[int]$bounds.Groups[4].Value)/2)
 $tree=Read-DeviceUi
}
$tree.SelectNodes('//node[@text!=""]') | ForEach-Object {
 if($_.password -eq 'true'){ '[password field] '+$_.bounds }else{ $_.text+' '+$_.bounds }
}
