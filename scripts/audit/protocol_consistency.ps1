$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [string]$File,
        [string]$Pattern,
        [string]$Message
    )

    if (-not (Select-String -Path $File -Pattern $Pattern -Quiet)) {
        throw "FAILED: $Message (`$File=$File, `$Pattern=$Pattern)"
    }
}

Write-Host "[Audit] Checking EEG source options..."
Assert-Contains -File "src/UI/ViewModels/WaveformViewModel.cs" -Pattern 'CH1 \(C3-P3\)' -Message "CH1 source option missing"
Assert-Contains -File "src/UI/ViewModels/WaveformViewModel.cs" -Pattern 'CH2 \(C4-P4\)' -Message "CH2 source option missing"
Assert-Contains -File "src/UI/ViewModels/WaveformViewModel.cs" -Pattern 'CH3 \(P3-P4\)' -Message "CH3 source option missing"
Assert-Contains -File "src/UI/ViewModels/WaveformViewModel.cs" -Pattern 'CH4 \(C3-C4' -Message "CH4 source option missing"

Write-Host "[Audit] Checking NIRS protocol config guard..."
Assert-Contains -File "src/DataSources/Rs232/Rs232TimeSeriesSource.cs" -Pattern 'BaudRate must be 57600' -Message "NIRS baudrate guard missing"
Assert-Contains -File "src/DataSources/Rs232/Rs232TimeSeriesSource.cs" -Pattern 'DataBits must be 8' -Message "NIRS databits guard missing"
Assert-Contains -File "src/DataSources/Rs232/Rs232TimeSeriesSource.cs" -Pattern 'StopBits must be One' -Message "NIRS stop bits guard missing"
Assert-Contains -File "src/DataSources/Rs232/Rs232TimeSeriesSource.cs" -Pattern 'Parity must be None' -Message "NIRS parity guard missing"

Write-Host "[Audit] Checking CRC vector test..."
Assert-Contains -File "tests/DataSources.Tests/Rs232ProtocolParserTests.cs" -Pattern '0x31C3' -Message "CRC-16 CCITT test vector missing"

Write-Host "[Audit] PASS"
