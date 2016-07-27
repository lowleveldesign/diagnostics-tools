param(
    [switch]$ThreadStates = $true,
    [switch]$ThreadWaitReasons = $false,
    [string]$ProcMask = "*"
)

if ($ThreadWaitReasons) {
    $ThreadStates = $false
    $label =
"Legend:
 0  - Waiting for a component of the Windows NT Executive| 1  - Waiting for a page to be freed
 2  - Waiting for a page to be mapped or copied          | 3  - Waiting for space to be allocated in the paged or nonpaged pool
 4  - Waiting for an Execution Delay to be resolved      | 5  - Suspended
 6  - Waiting for a user request                         | 7  - Waiting for a component of the Windows NT Executive
 8  - Waiting for a page to be freed                     | 9  - Waiting for a page to be mapped or copied
 10 - Waiting for space to be allocated in the paged or nonpaged pool| 11 - Waiting for an Execution Delay to be resolved
 12 - Suspended                                          | 13 - Waiting for a user request
 14 - Waiting for an event pair high                     | 15 - Waiting for an event pair low
 16 - Waiting for an LPC Receive notice                  | 17 - Waiting for an LPC Reply notice
 18 - Waiting for virtual memory to be allocated         | 19 - Waiting for a page to be written to disk"
    $counter = "\Thread(*)\Thread Wait Reason"
    $formats = @{Expression={$_.Key}; Label="Process Name"; width=15}, `
               @{Expression={$_.Value[0]}; Label="0"; width = 3}, `
               @{Expression={$_.Value[1]}; Label="1"; width = 3}, `
               @{Expression={$_.Value[2]}; Label="2"; width = 3}, `
               @{Expression={$_.Value[3]}; Label="3"; width = 3}, `
               @{Expression={$_.Value[4]}; Label="4"; width = 3}, `
               @{Expression={$_.Value[5]}; Label="5"; width = 3}, `
               @{Expression={$_.Value[6]}; Label="6"; width = 3}, `
               @{Expression={$_.Value[7]}; Label="7"; width = 3}, `
               @{Expression={$_.Value[8]}; Label="8"; width = 3}, `
               @{Expression={$_.Value[9]}; Label="9"; width = 3}, `
               @{Expression={$_.Value[10]}; Label="10"; width = 3}, `
               @{Expression={$_.Value[11]}; Label="11"; width = 3}, `
               @{Expression={$_.Value[12]}; Label="12"; width = 3}, `
               @{Expression={$_.Value[13]}; Label="13"; width = 3}, `
               @{Expression={$_.Value[14]}; Label="14"; width = 3}, `
               @{Expression={$_.Value[15]}; Label="15"; width = 3}, `
               @{Expression={$_.Value[16]}; Label="16"; width = 3}, `
               @{Expression={$_.Value[17]}; Label="17"; width = 3}, `
               @{Expression={$_.Value[18]}; Label="18"; width = 3}, `
               @{Expression={$_.Value[19]}; Label="19"; width = 3}

}

if ($ThreadStates) {
    $label = "Threads states / process"
    $counter = "\Thread(*)\Thread State"
    $formats = @{Expression={$_.Key}; Label="Process Name"; width=15}, `
               @{Expression={$_.Value[0]}; Label="Initialized"; width = 11}, `
               @{Expression={$_.Value[1]}; Label="Ready"; width = 11}, `
               @{Expression={$_.Value[2]}; Label="Running"; width = 11}, `
               @{Expression={$_.Value[3]}; Label="Standby"; width = 11}, `
               @{Expression={$_.Value[4]}; Label="Terminated"; width = 11}, `
               @{Expression={$_.Value[5]}; Label="Waiting"; width = 11}, `
               @{Expression={$_.Value[6]}; Label="Transition"; width = 11}, `
               @{Expression={$_.Value[7]}; Label="Unknown"; width = 11}
}


$processNames = @()   # array of processnames
$processThreadStates = @{}    # hashtable<processname, int[] states>

function ParseHeader([string] $tpHeader) {
    $tokens = $tpHeader.Split('","', [StringSplitOptions]::RemoveEmptyEntries)
    for ($i = 0; $i -lt $tokens.Length; $i++) {
        if ($tokens[$i] -match "\\\\.*\\thread\((?<procname>.*)/\d+(?<procinst>#\d+)?\)\\") {
            $proc = "{0}{1}" -f $matches.procname,$matches.procinst
            $tokens[$i] = $proc
            if (-not $processThreadStates.Contains($proc) -and $proc -like $ProcMask) {
                $processThreadStates.Add($proc, [Array]::CreateInstance([int], $formats.Length))
            }
        } else {
            $tokens[$i] = $null
        }
    }
    Set-Variable -Name processNames -Value $tokens -Scope script
}

function ParseValueString([string] $tpValues) {
    # first we need to clear current thread state table
    foreach ($k in $processThreadStates.Keys) {
        $vals = $processThreadStates[$k]
        for ($i = 0; $i -lt $vals.Length; $i++) {
            $vals[$i] = 0
        }
    }

    # parse and aggregate values
    $tokens = $tpValues.Split('","', [StringSplitOptions]::RemoveEmptyEntries)
    for ($i = 0; $i -lt $tokens.Length; $i++) {
        if ($processNames[$i]) {
            $state = [int]$tokens[$i]
            if ($state -lt $formats.Length -and $processThreadStates.Contains($processNames[$i])) {
                $processThreadStates[$processNames[$i]][$state]++
            }
        }
    }
}

function PrintProcessTable() {
    Write-Host $label

    $processThreadStates | Format-Table $formats
}

while ($true) {
$isheader = $true
    & typeperf $counter -si 1 -sc 2 | % {
        if ($isheader) {
            if ($_ -like '"(PDH*') {
                ParseHeader($_)
                $isheader = $false
            }
        } elseif ($_ -like '"*') {
            ParseValueString($_)

            # print nice table
            Clear-Host
            PrintProcessTable
        }
    }
}
