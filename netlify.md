# Netlify Deployment

This project follows the generic `netlify-deploy` skill (`~/.claude/skills/netlify-deploy/`)
and is that skill's reference project — its config below is the known-working baseline
reused for new no-backend projects.

## How it's wired up

1. **GitHub ↔ Netlify connection**: the Netlify site is linked to this repo's GitHub
   repository (`Myricaulus/kidz2learn`, public). Netlify auto-registered the webhook, so
   every push to the production branch (**`master`**, this repo's default branch) triggers
   a new build + deploy automatically. No GitHub Actions or other CI workflow is involved.
2. **No `netlify.toml`** — build settings are configured directly in the Netlify UI (Site
   configuration → Build & deploy → Build settings):
   - **Base directory**: `/`
   - **Build command**: `dotnet publish -c Release -o build`
   - **Publish directory**: `build/wwwroot`
   - **Functions directory**: default (`netlify/functions`), unused — static, fully
     client-side app, no serverless functions.
3. **Runtime**: not pinned in Netlify UI; the .NET SDK version comes from `global.json`
   (currently `8.0.x` — see the `netlify-deploy` skill for why, and for the current
   verified-support status before bumping it).

## Notes for agents

- Don't create a GitHub Actions workflow or other deploy config here — the UI-based setup
  above is the intended, working setup.
- Don't add server-side code, API routes, or Netlify Functions unless the user explicitly
  asks for that — it would be a real architecture change, not a deploy tweak.
- Build settings live in the Netlify UI, not in this repo — flag that to the user rather
  than trying to edit a config file that doesn't exist.
- Before changing the .NET version in `global.json`, check the `netlify-deploy` skill's
  verified-support log first.
