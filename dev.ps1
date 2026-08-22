[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('restore', 'build', 'format', 'test', 'run', 'publish-wpf', 'run-wpf', 'solution-list', 'reference-list', 'verify-build-conventions')]
    [string] $Command,

    [string] $Project,

    [string] $Filter,

    [switch] $RefreshLocks
)

$ErrorActionPreference = 'Stop'
$composeArguments = @('compose', 'run', '--rm', '--no-deps', 'dev')

switch ($Command) {
    'restore' {
        $toolArguments = @('dotnet', 'restore', 'TradingBot.sln')
        if ($RefreshLocks) {
            $toolArguments += @('--force-evaluate', '-p:RestoreLockedMode=false')
        }
    }
    'build' {
        $toolArguments = @('dotnet', 'build', 'TradingBot.sln', '--configuration', 'Release', '--no-restore')
    }
    'format' {
        $toolArguments = @('dotnet', 'format', 'TradingBot.sln', '--verify-no-changes', '--no-restore')
    }
    'test' {
        $testTarget = if ($Project) { $Project } else { 'TradingBot.sln' }
        $toolArguments = @('dotnet', 'test', $testTarget, '--configuration', 'Release', '--no-build')
        if ($Filter) {
            $toolArguments += @('--filter', $Filter)
        }
    }
    'run' {
        & docker compose run --build --rm --no-deps -e Trading__SmokeMode=true trading-host
        exit $LASTEXITCODE
    }
    'publish-wpf' {
        & docker compose run --rm --no-deps dev dotnet publish src/Trading.UI.Wpf/Trading.UI.Wpf.csproj --configuration Release --no-restore --output /workspace/artifacts/wpf/win-x64
        exit $LASTEXITCODE
    }
    'run-wpf' {
        & $PSCommandPath publish-wpf
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        $executable = Join-Path $PSScriptRoot 'artifacts\wpf\win-x64\Trading.UI.Wpf.exe'
        if (-not (Test-Path -LiteralPath $executable)) { throw "Published WPF executable was not found at $executable." }
        $runId = [Guid]::NewGuid().ToString('N')
        $runtimeDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "LlmTrader\WpfTestRuns\$runId"
        $readyFile = Join-Path $runtimeDirectory 'ready.json'
        $shutdownFile = Join-Path $runtimeDirectory 'shutdown.json'
        [System.IO.Directory]::CreateDirectory($runtimeDirectory) | Out-Null
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new($executable)
        $startInfo.UseShellExecute = $false
        $startInfo.Environment['LLM_TRADER_WPF_TEST_PROFILE'] = '1'
        $startInfo.Environment['LLM_TRADER_WPF_RUN_ID'] = $runId
        $startInfo.Environment['LLM_TRADER_WPF_DATA_DIRECTORY'] = $runtimeDirectory
        $startInfo.Environment['LLM_TRADER_WPF_READY_FILE'] = $readyFile
        $startInfo.Environment['LLM_TRADER_WPF_SHUTDOWN_FILE'] = $shutdownFile
        $process = [System.Diagnostics.Process]::Start($startInfo)
        if ($null -eq $process) { throw 'The WPF process could not be started.' }
        try {
            $process.WaitForExit()
            if (-not (Test-Path -LiteralPath $shutdownFile)) { throw 'The WPF process did not publish its bounded shutdown signal.' }
            exit $process.ExitCode
        }
        finally {
            $process.Dispose()
            Remove-Item -LiteralPath $runtimeDirectory -Recurse -Force
        }
    }
    'solution-list' {
        $toolArguments = @('dotnet', 'sln', 'TradingBot.sln', 'list')
    }
    'reference-list' {
        $toolArguments = @(
            'bash',
            '-lc',
            'for project in src/*/*.csproj tests/*/*.csproj; do dotnet reference list --project "$project" || exit; done'
        )
    }
    'verify-build-conventions' {
        $toolArguments = @(
            'bash',
            '-lc',
            @'
set -o pipefail
verify_failure() {
    project="$1"
    expected="$2"
    output="$(dotnet build "$project" --configuration Release 2>&1)"
    status=$?
    printf '%s\n' "$output"
    if [ "$status" -eq 0 ]; then
        echo "Expected $project to fail, but it succeeded." >&2
        return 1
    fi
    printf '%s\n' "$output" | grep -q "$expected"
}
verify_failure tests/BuildConventionsFixtures/CompilerWarning/CompilerWarning.csproj CS1030 &&
verify_failure tests/BuildConventionsFixtures/PlatformCompatibility/PlatformCompatibility.csproj CA1416
'@
        )
    }
}

& docker @composeArguments @toolArguments
exit $LASTEXITCODE
