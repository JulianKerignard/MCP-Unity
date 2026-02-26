# MCP Unity — Claude Code Skills Installer (Windows)
# Copies skills to ~/.claude/skills/ for Claude Code to detect them as slash commands.
#
# Usage:
#   .\install.ps1          # Install all skills
#   .\install.ps1 -Remove  # Remove all MCP Unity skills

param([switch]$Remove)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Target = Join-Path $env:USERPROFILE ".claude\skills"

$Skills = @(
    "mcp-unity"
    "unity-planner"
    "unity-plan"
    "gdd"
    "tdd-unity"
    "level-design"
    "art-direction"
    "milestone"
    "unity-story"
    "unity-review"
)

if ($Remove) {
    Write-Host "Removing MCP Unity skills from $Target..."
    foreach ($skill in $Skills) {
        $dest = Join-Path $Target $skill
        if (Test-Path $dest) {
            Remove-Item -Recurse -Force $dest
            Write-Host "  Removed $skill"
        }
    }
    Write-Host "Done. Skills removed."
    exit 0
}

Write-Host "Installing MCP Unity skills to $Target..."
Write-Host ""

if (-not (Test-Path $Target)) {
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
}

foreach ($skill in $Skills) {
    $src = Join-Path $ScriptDir $skill
    $dest = Join-Path $Target $skill

    if (-not (Test-Path $src)) {
        Write-Host "  SKIP $skill (not found in package)"
        continue
    }

    if (Test-Path $dest) {
        Write-Host "  UPDATE $skill (overwriting existing)"
        Remove-Item -Recurse -Force $dest
    } else {
        Write-Host "  INSTALL $skill"
    }

    Copy-Item -Recurse $src $dest
}

Write-Host ""
Write-Host "Installed $($Skills.Count) skills:"
Write-Host ""
Write-Host "  Slash commands available:"
Write-Host "    /unity-plan     - Initialize Unity project planning"
Write-Host "    /gdd            - Create Game Design Document"
Write-Host "    /tdd-unity      - Create Technical Design Document"
Write-Host "    /level-design   - Plan level/scene structure"
Write-Host "    /art-direction  - Define art style and materials"
Write-Host "    /milestone      - Plan game milestones"
Write-Host "    /unity-story    - Create implementation stories with MCP tools"
Write-Host "    /unity-review   - Audit project architecture"
Write-Host ""
Write-Host "  Reference skills (loaded automatically):"
Write-Host "    mcp-unity       - 164 MCP Unity tools reference"
Write-Host "    unity-planner   - Shared templates, agents, workflows"
Write-Host ""
Write-Host "Restart Claude Code for skills to take effect."
