# IdleCloud Web Deployment

## Hosting target

The production site is deployed as a static Vite bundle through GitHub Pages. The workflow is [deploy-idlecloud-web.yml](../.github/workflows/deploy-idlecloud-web.yml).

> GitHub Pages is unavailable for this private repository on its current GitHub plan. The workflow deliberately skips deployment while the repository is private. To use GitHub Pages, make the repository public or upgrade to a GitHub plan that supports private Pages sites. Keep the repository private and use another authenticated static host if the source must remain private.

## One-time repository setup

1. Ensure the repository is public or on a plan that supports private Pages sites.
2. Push this repository to GitHub on `main` or `master`.
3. In **Settings → Pages**, set **Source** to **GitHub Actions**.
4. Push a change affecting `webapp/`, or run **Deploy IdleCloud Web** from the repository Actions page.
5. Use the `github-pages` environment URL emitted by the workflow as the production URL.

The workflow runs tests before creating and uploading the static `webapp/dist` artifact. It receives the correct Vite base path from GitHub Pages, covering both project sites and root-hosted Pages sites. To override that value for a custom deployment path, run the workflow manually and set `base_path`.

## Local release gate

Use Node.js 22.14 or later. From `webapp/`, run `npm install` followed by `npm run check`. The `check` command runs Vitest and then the TypeScript/Vite production build.

## Content publication

Before a production gameplay release, open the Unity project and select **IdleCloud → Web → Export Content Snapshot**. The command writes the browser-shaped content payload to `src/content/generated/idlecloud-content.json`. Commit that generated payload with the content change so the deployment has a stable, reviewable content snapshot.

## Current environment limitation

This workspace downloaded a portable Node.js runtime successfully, but direct access to `registry.npmjs.org` is blocked by the current network policy. Therefore dependency installation, Vitest, and the local Vite build could not be run here. GitHub-hosted workflow runners install and validate dependencies independently during deployment.
