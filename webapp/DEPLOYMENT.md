# IdleCloud Web Deployment

## Hosting target

The production site is deployed as a static Vite bundle through GitHub Pages. The workflow is [deploy-idlecloud-web.yml](../.github/workflows/deploy-idlecloud-web.yml).

The repository is public and GitHub Pages is configured to use GitHub Actions as its publishing source.

## One-time repository setup

1. Push this repository to GitHub on `main` or `master`.
2. In **Settings → Pages**, set **Source** to **GitHub Actions**.
3. Push a change affecting `webapp/`, or run **Deploy IdleCloud Web** from the repository Actions page.
4. Use the `github-pages` environment URL emitted by the workflow as the production URL.

The workflow runs tests before creating and uploading the static `webapp/dist` artifact. It receives the correct Vite base path from GitHub Pages, covering both project sites and root-hosted Pages sites. To override that value for a custom deployment path, run the workflow manually and set `base_path`.

## Local release gate

Use Node.js 22.14 or later. From `webapp/`, run `npm install` followed by `npm run check`. The `check` command runs Vitest and then the TypeScript/Vite production build.

## Content publication

Before a production gameplay release, open the Unity project and select **IdleCloud → Web → Export Content Snapshot**. The command writes the browser-shaped content payload to `src/content/generated/idlecloud-content.json`. Commit that generated payload with the content change so the deployment has a stable, reviewable content snapshot.

## Current environment limitation

This workspace downloaded a portable Node.js runtime successfully, but direct access to `registry.npmjs.org` is blocked by the current network policy. Therefore dependency installation, Vitest, and the local Vite build could not be run here. GitHub-hosted workflow runners install and validate dependencies independently during deployment.
