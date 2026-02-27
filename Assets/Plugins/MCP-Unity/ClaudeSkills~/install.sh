#!/bin/bash
# MCP Unity — Claude Code Skills Installer
# Copies skills for Claude Code to detect them as slash commands.
#
# Usage:
#   bash install.sh              # Install to project .claude/skills/ (recommended)
#   bash install.sh --global     # Install to ~/.claude/skills/ (all projects)
#   bash install.sh --remove     # Remove from project .claude/skills/
#   bash install.sh --remove --global  # Remove from ~/.claude/skills/

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GLOBAL=false
REMOVE=false

# Parse arguments
for arg in "$@"; do
  case "$arg" in
    --global) GLOBAL=true ;;
    --remove) REMOVE=true ;;
  esac
done

# Resolve target: local (project) or global (~/.claude/skills/)
if [ "$GLOBAL" = true ]; then
  TARGET="$HOME/.claude/skills"
  SCOPE="global (~/.claude/skills/)"
else
  # Walk up from ClaudeSkills~ to find project root (parent of Assets/)
  ASSETS_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
  PROJECT_ROOT="$(dirname "$ASSETS_DIR")"
  TARGET="$PROJECT_ROOT/.claude/skills"
  SCOPE="project ($TARGET)"
fi

# All skill directories to install
SKILLS=(
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

if [ "$REMOVE" = true ]; then
  echo "Removing MCP Unity skills from $SCOPE..."
  for skill in "${SKILLS[@]}"; do
    if [ -d "$TARGET/$skill" ]; then
      rm -rf "$TARGET/$skill"
      echo "  Removed $skill"
    fi
  done
  echo "Done. Skills removed."
  exit 0
fi

echo "Installing MCP Unity skills to $SCOPE..."
echo ""

mkdir -p "$TARGET"

for skill in "${SKILLS[@]}"; do
  src="$SCRIPT_DIR/$skill"
  dest="$TARGET/$skill"

  if [ ! -d "$src" ]; then
    echo "  SKIP $skill (not found in package)"
    continue
  fi

  if [ -d "$dest" ]; then
    echo "  UPDATE $skill (overwriting existing)"
    rm -rf "$dest"
  else
    echo "  INSTALL $skill"
  fi

  cp -r "$src" "$dest"
done

echo ""
echo "Installed ${#SKILLS[@]} skills to $SCOPE"
echo ""
echo "  Slash commands available:"
echo "    /unity-plan     — Initialize Unity project planning"
echo "    /gdd            — Create Game Design Document"
echo "    /tdd-unity      — Create Technical Design Document"
echo "    /level-design   — Plan level/scene structure"
echo "    /art-direction  — Define art style and materials"
echo "    /milestone      — Plan game milestones"
echo "    /unity-story    — Create implementation stories with MCP tools"
echo "    /unity-review   — Audit project architecture"
echo ""
echo "  Reference skills (loaded automatically):"
echo "    mcp-unity       — 164 MCP Unity tools reference"
echo "    unity-planner   — Shared templates, agents, workflows"
echo ""
echo "Restart Claude Code for skills to take effect."
