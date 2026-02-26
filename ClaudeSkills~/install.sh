#!/bin/bash
# MCP Unity — Claude Code Skills Installer
# Copies skills to ~/.claude/skills/ for Claude Code to detect them as slash commands.
#
# Usage:
#   bash install.sh          # Install all skills
#   bash install.sh --remove # Remove all MCP Unity skills

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET="$HOME/.claude/skills"

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

if [ "$1" = "--remove" ]; then
  echo "Removing MCP Unity skills from $TARGET..."
  for skill in "${SKILLS[@]}"; do
    if [ -d "$TARGET/$skill" ]; then
      rm -rf "$TARGET/$skill"
      echo "  Removed $skill"
    fi
  done
  echo "Done. Skills removed."
  exit 0
fi

echo "Installing MCP Unity skills to $TARGET..."
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
echo "Installed ${#SKILLS[@]} skills:"
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
