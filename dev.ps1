[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('restore', 'build', 'format', 'test', 'solution-list', 'reference-list', 'verify-build-conventions')]
    [string] $Command,

    [string] $Project,

    [string] $Filter
)

$ErrorActionPreference = 'Stop'
$composeArguments = @('compose', 'run', '--rm', '--no-deps', 'dev')

switch ($Command) {
    'restore' {
        $toolArguments = @('dotnet', 'restore', 'TradingBot.sln')
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
