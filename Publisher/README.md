# Publisher controls

Open [Service control in GitHub Actions](https://github.com/w-rakeeb/dual-steam/actions/workflows/service-policy.yml), choose **Run workflow**, select the state and enter the announcement.

| State | Next full startup |
| --- | --- |
| `active` | Authorize a current official build. |
| `paused` | Show the temporary-pause announcement and block Second Steam. |
| `shutdown` | Show the service-closed announcement and block Second Steam. |
| `keep` | Keep the existing state while refreshing the required public release/message. |

Every published **stable GitHub release** updates the signed minimum version and verified ZIP metadata automatically. A source-code push, draft or prerelease does not require an update. Upload the Windows ZIP with its GitHub SHA-256 digest before publishing the stable release. The workflow must succeed for a new policy to take effect; check its result after publishing.

Policy changes apply at the next full manager startup. Existing authorized sessions keep working; minimizing to the tray is not a restart. Full exit ends the session and Second Steam requires the manager. A new startup needs internet access and a valid publisher signature. The second controller checks the actual running manager over a local process-bound authorization endpoint; a copied session cache is not sufficient. API/raw-content availability and GitHub propagation can affect when a change is received. Main Steam is independent.

The signing key is stored in the repository secret `DUAL_STEAM_POLICY_KEY`; only a public verification key is compiled into the app. The private publisher backup is outside the portable app folder. Do not commit or distribute it. No client account IDs, Steam credentials or user statistics are uploaded.

These are controls for unmodified official builds from v3.3.0 onward. They cannot retroactively control older versions, revoke Steam ownership, or prevent a local administrator from modifying/open-sourcing their own copy. Closing the publisher's own local application does not change the public policy; run this workflow to pause or close service access.
