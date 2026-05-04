Set objFSO = CreateObject("Scripting.FileSystemObject")
strDir = "C:\Users\dell\source\repos\TasteVinhKhanh-SmartAudioNavigation\docs_full\sequence"
Dim deleted
deleted = 0

For i = 1 To 19
    strPattern = Right("0" & i, 2) & "-"
    Set objFolder = objFSO.GetFolder(strDir)
    Set objFiles = objFolder.Files
    For Each objFile In objFiles
        If Left(objFile.Name, 3) = strPattern Then
            objFSO.DeleteFile objFile.Path
            deleted = deleted + 1
        End If
    Next
Next

WScript.Echo "Deleted " & deleted & " old sequence files"
