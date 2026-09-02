' ============================================================
' Create-DesktopShortcuts.vbs
'
' Vytvoří na ploše aktuálního uživatele dva zástupce:
'   1) "Chrome (host)"   - spustí Chrome v režimu Host (--guest)
'   2) "Spustit kiosk"   - spustí KioskMeet aplikaci (KioskMeet.exe)
'
' PŘED SPUŠTĚNÍM: uprav proměnnou kioskExePath níže, ať ukazuje na
' skutečné umístění KioskMeet.exe na tomto počítači.
'
' Spuštění: poklepáním na tento soubor (dvojklik).
' ============================================================

Option Explicit

Dim oWS, oFSO, strDesktop
Dim chromePath, chromePathX86, kioskExePath
Dim oLink

Set oWS = CreateObject("WScript.Shell")
Set oFSO = CreateObject("Scripting.FileSystemObject")

' ---- Nastavení cest (uprav dle potřeby) ----
chromePath    = "C:\Program Files\Google\Chrome\Application\chrome.exe"
chromePathX86 = "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
kioskExePath  = "C:\KioskMeet\KioskMeet.exe"   ' <-- uprav na skutečnou cestu

' ---- Najít správnou cestu k Chromu ----
If Not oFSO.FileExists(chromePath) Then
    If oFSO.FileExists(chromePathX86) Then
        chromePath = chromePathX86
    Else
        MsgBox "Chrome nebyl nalezen na obvyklém místě." & vbCrLf & _
               "Zástupce na Chrome (host) se přesto vytvoří, ale bude " & _
               "potřeba cestu v jeho vlastnostech opravit ručně.", _
               vbExclamation, "Upozornění"
    End If
End If

strDesktop = oWS.SpecialFolders("Desktop")

' ---- Zástupce 1: Chrome (host) ----
Set oLink = oWS.CreateShortcut(strDesktop & "\Chrome (host).lnk")
oLink.TargetPath = chromePath
oLink.Arguments = "--guest"
oLink.IconLocation = chromePath & ",0"
oLink.Description = "Otevre Chrome v rezimu Host (bez prihlaseni a historie)"
oLink.WindowStyle = 1
oLink.Save

' ---- Zástupce 2: Spustit kiosk ----
If Not oFSO.FileExists(kioskExePath) Then
    MsgBox "KioskMeet.exe nebyl nalezen na cestě:" & vbCrLf & kioskExePath & vbCrLf & vbCrLf & _
           "Zástupce se přesto vytvoří - oprav prosím cestu přímo v tomto " & _
           "skriptu (proměnná kioskExePath) a spusť ho znovu, nebo uprav " & _
           "cestu ručně ve vlastnostech zástupce na ploše.", _
           vbExclamation, "Upozornění"
End If

Set oLink = oWS.CreateShortcut(strDesktop & "\Spustit kiosk.lnk")
oLink.TargetPath = kioskExePath
oLink.WorkingDirectory = oFSO.GetParentFolderName(kioskExePath)
oLink.IconLocation = kioskExePath & ",0"
oLink.Description = "Spusti KioskMeet aplikaci"
oLink.WindowStyle = 1
oLink.Save

MsgBox "Hotovo! Na ploše byly vytvořeny zástupci:" & vbCrLf & _
       "  - Chrome (host)" & vbCrLf & _
       "  - Spustit kiosk", _
       vbInformation, "Zástupci vytvořeni"

' ---- Volitelně: vytvořit stejné zástupce i pro všechny uživatele ----
' (vyžaduje spuštění tohoto skriptu jako Správce - jinak zápis do
' sdílené plochy selže)
Dim answer
answer = MsgBox("Chceš vytvořit stejné zástupce i pro VŠECHNY uživatele " & _
                "tohoto počítače (sdílená plocha)?" & vbCrLf & vbCrLf & _
                "Vyžaduje spuštění tohoto skriptu jako Správce.", _
                vbQuestion + vbYesNo, "Zástupci pro všechny uživatele")

If answer = vbYes Then
    Dim strAllUsersDesktop
    On Error Resume Next
    strAllUsersDesktop = oWS.SpecialFolders("AllUsersDesktop")
    On Error Goto 0

    If strAllUsersDesktop = "" Then
        MsgBox "Nepodařilo se najít sdílenou plochu.", vbCritical, "Chyba"
    Else
        On Error Resume Next

        Set oLink = oWS.CreateShortcut(strAllUsersDesktop & "\Chrome (host).lnk")
        oLink.TargetPath = chromePath
        oLink.Arguments = "--guest"
        oLink.IconLocation = chromePath & ",0"
        oLink.Description = "Otevre Chrome v rezimu Host (bez prihlaseni a historie)"
        oLink.WindowStyle = 1
        oLink.Save

        Set oLink = oWS.CreateShortcut(strAllUsersDesktop & "\Spustit kiosk.lnk")
        oLink.TargetPath = kioskExePath
        oLink.WorkingDirectory = oFSO.GetParentFolderName(kioskExePath)
        oLink.IconLocation = kioskExePath & ",0"
        oLink.Description = "Spusti KioskMeet aplikaci"
        oLink.WindowStyle = 1
        oLink.Save

        If Err.Number <> 0 Then
            MsgBox "Zápis na sdílenou plochu selhal (chyba: " & Err.Description & ")." & vbCrLf & _
                   "Spusť tento skript znovu jako Správce (pravé tlačítko -> " & _
                   "Spustit jako správce, případně přes cmd.exe / PowerShell " & _
                   "spuštěný jako správce a příkaz: cscript Create-DesktopShortcuts.vbs).", _
                   vbCritical, "Chyba zápisu"
        Else
            MsgBox "Zástupci byli vytvořeni i na sdílené ploše pro všechny uživatele.", _
                   vbInformation, "Hotovo"
        End If

        On Error Goto 0
    End If
End If
