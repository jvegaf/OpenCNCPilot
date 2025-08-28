#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
PROMPTS_DIR="$ROOT_DIR/.github/prompts"

mkdir -p "$PROMPTS_DIR"

BASE_URL="https://raw.githubusercontent.com/github/awesome-copilot/main/prompts"

FILES=(
  "review-and-refactor.prompt.md"
  "breakdown-plan.prompt.md"
  "create-github-action-workflow-specification.prompt.md"
  "create-github-issue-feature-from-specification.prompt.md"
  "create-github-issues-feature-from-implementation-plan.prompt.md"
  "create-github-pull-request-from-specification.prompt.md"
  "csharp-xunit.prompt.md"
  "dotnet-best-practices.prompt.md"
  "generate-custom-instructions-from-codebase.prompt.md"
  "code-exemplars-blueprint-generator.prompt.md"
)

echo "Updating prompts in $PROMPTS_DIR"
for f in "${FILES[@]}"; do
  echo "- Fetching $f"
  curl -fsSL "$BASE_URL/$f" -o "$PROMPTS_DIR/$f"
done

# Fetch license for third-party notices
curl -fsSL "https://raw.githubusercontent.com/github/awesome-copilot/main/LICENSE" -o "$PROMPTS_DIR/LICENSE.awesome-copilot"

echo "Done."
