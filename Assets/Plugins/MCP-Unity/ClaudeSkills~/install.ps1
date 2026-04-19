# MCP Unity — Claude Code Skills Installer (Windows)
# Copies skills for Claude Code to detect them as slash commands.
#
# Usage:
#   .\install.ps1                # Install to project .claude/skills/ (recommended)
#   .\install.ps1 -Global        # Install to ~/.claude/skills/ (all projects)
#   .\install.ps1 -Remove         # Remove from project .claude/skills/
#   .\install.ps1 -Remove -Global # Remove from ~/.claude/skills/

param(
    [switch]$Remove,
    [switch]$Global
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Resolve target: local (project) or global (~/.claude/skills/)
if ($Global) {
    $Target = Join-Path $env:USERPROFILE ".claude\skills"
    $Scope = "global (~/.claude/skills/)"
} else {
    # Walk up from ClaudeSkills~ to find project root (parent of Assets/)
    $AssetsDir = Split-Path -Parent (Split-Path -Parent $ScriptDir)
    $ProjectRoot = Split-Path -Parent $AssetsDir
    $Target = Join-Path $ProjectRoot ".claude\skills"
    $Scope = "project ($Target)"
}

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
    Write-Host "Removing MCP Unity skills from $Scope..."
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

Write-Host "Installing MCP Unity skills to $Scope..."
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
Write-Host "Installed $($Skills.Count) skills to $Scope"
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
