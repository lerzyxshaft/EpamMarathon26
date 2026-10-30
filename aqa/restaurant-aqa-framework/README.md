# Restaurant AQA Framework

**Direction:** 1 — end-to-end testing of the deployed group pre-order application (backend and frontend).
**Levels:** 1, 2, and 3.
**Architecture:** NUnit + Playwright for .NET, with Page Objects for UI behavior and an HTTP API client for setup, authorization, and state checks.

The demo UI tests are safe to run against the public deployment. Live UI/API scenarios are opt-in and require a dedicated test table and test credentials. They create a fresh reservation per test, choose a free time slot dynamically, and remove guest sessions, the pre-order, and the reservation during teardown.

## Requirements

- .NET 10 SDK
- PowerShell (`pwsh`) to install Playwright's browser, or Playwright Chromium already installed
- A deployed application for demo UI tests
- For live scenarios: an owner account, a dedicated table with capacity of at least four, and an available dish at that table's location
- Optional Report Portal server/project and a Report Portal API key

## Local setup

From the repository root:

```bash
cd aqa/restaurant-aqa-framework
cp .env.example .env
dotnet restore
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test
```

`.env` is ignored by Git. Set `AQA_FRONTEND_URL` and `AQA_API_URL` to the environment you intend to test. Demo UI tests use only the public `?demo` flow and do not call the backend.

To enable live tests, configure these entries in `.env`:

```dotenv
AQA_RUN_LIVE_TESTS=true
AQA_OWNER_EMAIL=your-test-owner@example.com
AQA_OWNER_PASSWORD=your-test-password
AQA_TEST_TABLE_ID=dedicated-test-table-id
AQA_TEST_DISH_ID=available-dish-id
```

The configured table must be dedicated to automation and have capacity for four guests. Live tests create real reservations and orders in the configured environment. They clean up after successful setup; if the runner is terminated forcefully, remove any test reservation left behind before rerunning.

An optional second customer account enables the non-owner authorization scenario:

```dotenv
AQA_OTHER_CUSTOMER_EMAIL=other-test-customer@example.com
AQA_OTHER_CUSTOMER_PASSWORD=another-test-password
```

## Test levels

Select a level or subset with NUnit categories:

```bash
dotnet test --filter "TestCategory=L1"
dotnet test --filter "TestCategory=L2"
dotnet test --filter "TestCategory=L3"
```

- **L1:** participant baskets, per-participant and aggregate totals, submitted status, authentication/ownership, and unknown groups.
- **L2:** consolidated submit, duplicate dish aggregation, excluding unsubmitted baskets, empty basket/session and missing-order errors, and repeat submission.
- **L3:** updated snapshots after deletion, deleting the last participant, nonexistent and cross-group participants, and unauthenticated deletion.

The live tests deliberately use fresh reservations and are serialized to avoid table-slot conflicts. Tests requiring live AWS data are skipped unless all live-test settings are enabled.

## Report Portal and CI

The `ReportPortal.NUnit` agent is included. For local reporting, set the following values in `.env`; the test assembly maps them to the agent's environment-based configuration before NUnit starts:

```dotenv
RP_ENDPOINT=https://your-report-portal.example.com
RP_API_KEY=your-report-portal-user-api-key
RP_PROJECT=your-project
RP_LAUNCH=Restaurant AQA
```

The GitHub Actions workflow is `.github/workflows/aqa-e2e.yml`. It runs on GitHub-hosted runners, installs Chromium, writes TRX results, and uploads reports plus failure screenshots and Playwright traces before marking the job failed when tests fail.

To enable live scenarios in CI, add repository variables `AQA_RUN_LIVE_TESTS=true`, `AQA_TEST_TABLE_ID`, and `AQA_TEST_DISH_ID`, and secrets `AQA_OWNER_EMAIL` and `AQA_OWNER_PASSWORD`. Optional non-owner checks use `AQA_OTHER_CUSTOMER_EMAIL` and `AQA_OTHER_CUSTOMER_PASSWORD`. Set `RP_ENDPOINT`, `RP_API_KEY`, and `RP_PROJECT` to enable Report Portal reporting. Never commit credentials or AWS keys.

## Undefined behavior

- Submitting a group twice is expected to return HTTP 400 because only an open group can be submitted.
- Deleting the final guest is expected to return an empty participant list and total `0`.
- A guest cart with no dishes cannot be submitted; the API rejects it before group submission.
- The backend excludes owner sessions from the guest snapshot and consolidates submitted guest baskets only.

The sample/demo flow is intentionally isolated: its submission is local-only and does not create an order in the deployed backend.
