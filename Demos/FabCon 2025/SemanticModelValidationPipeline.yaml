trigger: none

pool:
  vmimage: 'windows-latest'

stages:
 - stage: Build
   jobs:

    - job: Build_Datasets
      steps:
        - checkout: self
          path: 'self'
        - task: PowerShell@2
          displayName: 'Download Tabular Editor and Default Rules'
          inputs:
            targetType: inline
            script: |     
              $path = "$(Build.SourcesDirectory)"                       
              $tempPath = "$path\_temp"
              $toolPath = "$path\_tools\TE"
              New-Item -ItemType Directory -Path $tempPath -ErrorAction SilentlyContinue | Out-Null              
              
              Write-Host "##[debug]Downloading Tabular Editor binaries"
              $downloadUrl = "https://github.com/TabularEditor/TabularEditor/releases/latest/download/TabularEditor.Portable.zip"
              $zipFile = "$tempPath\TabularEditor.zip"
              Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile
              Expand-Archive -Path $zipFile -DestinationPath $toolPath -Force            

              Write-Host "##[debug]Downloading Dataset default rules"
              $downloadUrl = "https://raw.githubusercontent.com/microsoft/Analysis-Services/master/BestPracticeRules/BPARules.json"
              Invoke-WebRequest -Uri $downloadUrl -OutFile "$tempPath\Rules-Dataset.json"     
        - task: PowerShell@2
          displayName: 'Check invalid object refs'
          inputs:
            targetType: inline
            script: |
              $path = "$(Build.SourcesDirectory)"
              $tempPath = "$path\_temp"
              $toolPath = "$path\_Tools\TE\TabularEditor.exe"
              $scriptPath = "$path\scripts\CheckReportsForInvalidObjectRefs.csx"

              $itemsFolders = Get-ChildItem  -Path $path -recurse -include definition.pbism
              foreach($itemFolder in $itemsFolders)
              {	
                  $itemPath = "$($itemFolder.Directory.FullName)\definition\database.tmdl"
                  Write-Host "##[group]Running invalid object ref check for reports pointing to: '$itemPath'"
                  Start-Process -FilePath "$toolPath" -ArgumentList """$itemPath"" -s ""$scriptPath"" -V" -NoNewWindow -Wait
                  Write-Host "##[endgroup]"
              }
        - task: PowerShell@2
          displayName: 'Run Best Practice Analyzer'
          inputs:
            targetType: inline
            script: |
              $path = "$(Build.SourcesDirectory)"
              $tempPath = "$path\_temp"
              $toolPath = "$path\_Tools\TE\TabularEditor.exe"
              $rulesPath = "$path\Rules-Dataset.json"

              if (!(Test-Path $rulesPath))
              {
                  Write-Host "Running downloaded rules"
                  $rulesPath = "$tempPath\Rules-Dataset.json"
              }

              $itemsFolders = Get-ChildItem  -Path $path -recurse -include definition.pbism
              foreach($itemFolder in $itemsFolders)
              {	
                  $itemPath = "$($itemFolder.Directory.FullName)\definition\database.tmdl"
                  Write-Host "##[group]Running rules for: '$itemPath'"
                  Start-Process -FilePath "$toolPath" -ArgumentList """$itemPath"" -A ""$rulesPath"" -V" -NoNewWindow -Wait
                  Write-Host "##[endgroup]"
              }