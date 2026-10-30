# Restaurant Application

Restaurant Application is a cloud-hosted restaurant application deployed on AWS. It consists of an Angular frontend, .NET 10 serverless backend functions deployed with AWS Syndicate, and Terraform-managed infrastructure.

## Architecture

- **Frontend:** Angular 22 and Node.js application in `frontend/restaurant-frontend-app`. The production API endpoint is generated from `BACKEND_URL` by `scripts/set-env.mjs`; the build is uploaded to a private S3 bucket and distributed through CloudFront.
- **Backend:** .NET 10 Lambda functions in `backend/restaurant-backend-app`, packaged and deployed with the Python-based AWS Syndicate CLI (`aws-syndicate`). Deployment artifacts are stored in a dedicated S3 bucket.
- **Infrastructure:** Terraform provisions AWS resources in `us-west-2`, including the frontend S3 bucket, CloudFront distribution, IAM roles, and policies.
- **Terraform state:** Stored remotely in an S3 bucket named `restaurant-terraform-state-<ACCOUNT_ID>`.

## Prerequisites

Install and configure the following tools:

- AWS CLI v2, authenticated to the AWS account you will deploy into.
- Terraform 1.10 or later.
- .NET 10 SDK to build and test the backend.
- Python 3.13 and `pip` for AWS Syndicate.
- Node.js 22.22.3+ or 24.15.0+ and npm for the Angular frontend.
- AWS Syndicate: `pip install aws-syndicate`.

The AWS identity used for deployment needs permissions to manage the resources created by Terraform and AWS Syndicate, and read/write access to the Terraform state and deployment buckets. Prefer a dedicated, least-privilege deployment role. Broad policies such as `AdministratorAccess` should only be used temporarily in an isolated development account, never for routine or production access.

Before initializing Terraform, create the Terraform state bucket. It must be globally unique and located in `us-west-2`. Enable versioning and block public access.

> **Note:** Replace `<ACCOUNT_ID>` and other placeholders with actual values. Do not include the angle brackets or braces in AWS bucket names or Terraform configuration.

Verify the active AWS identity and region:

```bash
aws sts get-caller-identity
aws configure get region
```

## Deployment guide

Deploy the application in this order:

1. Provision the AWS infrastructure with Terraform.
2. Build and deploy the backend with AWS Syndicate.
3. Build and deploy the frontend to S3 and invalidate the CloudFront cache.

### 1. Deploy the infrastructure with Terraform

Create the Terraform state bucket if it does not already exist:

```bash
export AWS_REGION=us-west-2
export ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
export TF_STATE_BUCKET="restaurant-terraform-state-${ACCOUNT_ID}"

aws s3api create-bucket \
  --bucket "$TF_STATE_BUCKET" \
  --region "$AWS_REGION" \
  --create-bucket-configuration LocationConstraint="$AWS_REGION"

aws s3api put-bucket-versioning \
  --bucket "$TF_STATE_BUCKET" \
  --versioning-configuration Status=Enabled

aws s3api put-public-access-block \
  --bucket "$TF_STATE_BUCKET" \
  --public-access-block-configuration \
  'BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true'
```

If the bucket already exists, skip the `create-bucket` command and verify its versioning and public-access-block settings. Open `infra/terraform/backend.tf` and set `bucket` to the exact state bucket name. For example:

```hcl
terraform {
  backend "s3" {
    bucket       = "restaurant-terraform-state-123456789012"
    key          = "restaurant/terraform.tfstate"
    region       = "us-west-2"
    use_lockfile = true
    encrypt      = true
  }
}
```

Do not leave template values such as `{ACCOUNT_ID}`, `<ACCOUNT_ID>`, or `{your_account_id}` in the bucket name. Update the account ID and any account-specific values in `infra/terraform/variables.tf` before applying.

Run Terraform from the infrastructure directory:

```bash
cd infra/terraform
terraform init
terraform fmt -recursive
terraform validate
terraform plan
terraform apply -auto-approve
```

Terraform creates the frontend S3 bucket and CloudFront distribution, along with the IAM roles and policies and other infrastructure defined in the configuration.

> **Note:** Review the Terraform plan before applying it, especially in a shared or production AWS account. Remove `-auto-approve` if you want Terraform to ask for confirmation.

### 2. Deploy the backend with AWS Syndicate

Create and activate a Python virtual environment in the backend project directory:

```bash
cd ../../backend/restaurant-backend-app
python3 -m venv .venv
source .venv/bin/activate
```

Install AWS Syndicate and build the .NET solution:

```bash
pip install aws-syndicate
dotnet build restaurant-backend-app.sln
```

This repository's Lambda projects are .NET; it does not contain a backend `requirements.txt`. Python is used to install and run the Syndicate CLI.

Create an S3 bucket for Syndicate deployment artifacts. The bucket name must be globally unique:

```bash
export DEPLOY_BUCKET="restaurant-deploy-bucket-${ACCOUNT_ID}"

aws s3api create-bucket \
  --bucket "$DEPLOY_BUCKET" \
  --region "$AWS_REGION" \
  --create-bucket-configuration LocationConstraint="$AWS_REGION"
```

If the bucket already exists, skip the creation command. Generate the Syndicate configuration with the deployment bucket:

```bash
syndicate generate config \
  --name dev \
  --region us-west-2 \
  --deploy-target-bucket "$DEPLOY_BUCKET"
```

Syndicate-generated aliases contain only a few defaults. Copy the project's complete alias set into the generated configuration directory and update account-specific values before building:

```bash
export SDCT_CONF="$(pwd)/.syndicate-config-dev"
cp .syndicate-config-restaurant-backend-app/syndicate_aliases.yml \
  "$SDCT_CONF/syndicate_aliases.yml"
```

In `syndicate.yml`, verify `account_id`, `project_path`, and `deploy_target_bucket`. In `syndicate_aliases.yml`, replace account-specific bucket names and make sure every `${alias}` used in `deployment_resources.json` has a matching key. Use the Cognito **pool name** (`restaurant-backend-user-pool`) for `user_pool_name`; Syndicate resolves that name to the pool ID when configuring the API authorizer.

Lambda `env_variables` in `lambda_config.json` are not expanded from `syndicate_aliases.yml`. Set these values to their concrete resource names (table/index, queue, bucket, and region). For Cognito dynamic values, set `resource_name` to the user-pool name and keep `resource_type: cognito_idp` and the relevant `parameter`.

Build a fresh bundle and deploy it:

```bash
syndicate build --bundle-name 1.0.0
syndicate deploy --deploy-name dev --bundle-name 1.0.0
```

Use a new bundle name for each build; Syndicate does not overwrite an existing bundle. For an already-deployed application, use `syndicate update` to apply changes to existing Lambda functions rather than expecting `syndicate deploy` to update them:

```bash
syndicate update \
  --deploy-name dev \
  --bundle-name 1.0.0 \
  --update-only-types lambda \
  --replace-output
```

> **Note:** `SDCT_CONF` must point to the directory containing both `syndicate.yml` and `syndicate_aliases.yml`. Do not use the `.syndicate` project-state file as the configuration path.

After deployment, retrieve the API Gateway ID and base URL:

```bash
API_ID="$(aws apigateway get-rest-apis \
  --region us-west-2 \
  --query "items[?name=='restaurant-backend-api'].id | [0]" \
  --output text)"
printf 'https://%s.execute-api.us-west-2.amazonaws.com/develop\n' "$API_ID"
```

### 3. Build and deploy the Angular frontend

The frontend project is `frontend/restaurant-frontend-app`. Use Node.js 22.22.3+ or 24.15.0+ (the supported versions are declared in `package.json`):

```bash
cd ../../frontend/restaurant-frontend-app
npm ci
```

This is an Angular application, not a Create React App project: it does not read `.env` or `REACT_APP_API_URL`. Before production build, set `BACKEND_URL` to the API Gateway base URL used by the target environment. The npm `prebuild` hook runs `scripts/set-env.mjs`, which validates that URL and generates the ignored `src/environments/environment.production.ts` file:

```bash
export AWS_REGION=us-west-2
export BACKEND_URL="https://eknqcvq3l1.execute-api.us-west-2.amazonaws.com/develop"
npm run build
```

The Angular production output is `dist/restaurant-frontend-app/browser`. Sync this directory to the Terraform-managed private frontend bucket, then invalidate CloudFront:

The currently working frontend uses the same data-backed API as local development:
`https://eknqcvq3l1.execute-api.us-west-2.amazonaws.com/develop`. The API created in this
AWS account (`https://44hns6u8zf.execute-api.us-west-2.amazonaws.com/develop`) currently
returns empty `locations` and `dishes` collections. Keep using the data-backed API unless
you have populated the account's DynamoDB tables and verified the application against them.

```bash
export ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
export FRONTEND_BUCKET="restaurant-frontend-${ACCOUNT_ID}"
aws s3 sync dist/restaurant-frontend-app/browser/ \
  "s3://${FRONTEND_BUCKET}/" --delete

aws cloudfront create-invalidation \
  --distribution-id ENDHAUUPNZ0AU \
  --paths '/*'
```

The deployed CloudFront URL is `https://d1n6pycnjfkr4j.cloudfront.net/`. The distribution ID can also be queried with Terraform from `infra/terraform`:

```bash
cd ../../infra/terraform
terraform output -raw cloudfront_distribution_domain_name
terraform output -raw cloudfront_distribution_id
```

> **Note:** `aws s3 sync --delete` removes objects absent from the local build output. Confirm the AWS account, target bucket, and build path before using it.

## Fast updates and health checks

### Update only the frontend

Run from the repository root. Set `BACKEND_URL` to the current backend API URL, then build, upload, and invalidate CloudFront:

```bash
cd frontend/restaurant-frontend-app
export AWS_REGION=us-west-2
export ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
export API_ID="$(aws apigateway get-rest-apis \
  --region "$AWS_REGION" \
  --query "items[?name=='restaurant-backend-api'].id | [0]" \
  --output text)"
export BACKEND_URL="https://eknqcvq3l1.execute-api.us-west-2.amazonaws.com/develop"

npm ci
npm run build
aws s3 sync dist/restaurant-frontend-app/browser/ \
  "s3://restaurant-frontend-${ACCOUNT_ID}/" --delete
aws cloudfront create-invalidation \
  --distribution-id ENDHAUUPNZ0AU \
  --paths '/*'
```

### Update only the backend

After changing backend source or Syndicate resource configuration, use a fresh bundle name, then update the deployed resources. When changing Lambda config or environment variables, update Lambda resources explicitly:

```bash
cd backend/restaurant-backend-app
source .venv/bin/activate
export SDCT_CONF="$(pwd)/.syndicate-config-dev"
dotnet build restaurant-backend-app.sln
syndicate build --bundle-name 1.0.4
syndicate update \
  --deploy-name dev \
  --bundle-name 1.0.4 \
  --update-only-types lambda \
  --replace-output
```

Use `syndicate deploy` for provisioning resources not yet deployed; `syndicate update` applies configuration/code changes to existing resources. Ensure every deployment-resource placeholder is defined in the aliases loaded from `SDCT_CONF`.

### Check service health

```bash
curl -i https://d1n6pycnjfkr4j.cloudfront.net/
curl -i https://44hns6u8zf.execute-api.us-west-2.amazonaws.com/develop/locations
aws lambda get-function-configuration \
  --function-name api-handler:dev \
  --region us-west-2 \
  --query '{State:State,Version:Version,Environment:Environment.Variables}' \
  --output json
aws dynamodb describe-table \
  --table-name Locations \
  --region us-west-2 \
  --query 'Table.{Name:TableName,Status:TableStatus}' \
  --output table
aws logs tail /aws/lambda/api-handler --region us-west-2 --since 15m
```

The API response may be an empty array when a table has no records; HTTP 200 still confirms the request completed successfully. Treat non-2xx API responses and Lambda exceptions as incidents to investigate in CloudWatch.

## Dependency and IAM security checks

Run the frontend dependency audit from the Angular project directory:

```bash
cd frontend/restaurant-frontend-app
npm audit
```

For a conservative update, work in a branch, review available non-breaking fixes, apply only updates allowed by the existing semver ranges (do not use `--force`), then run `npm audit`, `npm run lint`, and `npm run build` with `BACKEND_URL` set. The production build's `prebuild` hook executes `scripts/set-env.mjs`; `postbuild` checks that the output contains no configuration placeholders. Review `package-lock.json` and verify the production API URL before deploying the bundle.

> **Note:** The last full-tree audit found 31 advisories (3 critical, 17 high, 11 moderate). The production-only audit found 7 (0 critical, 2 high, 5 moderate); the full-tree critical findings are in the build/development dependency tree, but still matter to developer workstations and CI. `npm audit fix --dry-run` previewed a broad dependency-tree change (175 packages added, 54 removed, 48 changed) and continued to report the existing audit findings. Do not apply that change blindly—update on a branch, review the lockfile, build/test, and re-audit before deployment.

IAM requires least-privilege hardening:

- The `lambda-basic-execution` policy is shared by all three Lambda roles and grants data permissions with `Resource: "*"`, including S3 object writes/deletes, DynamoDB CRUD/Scan, KMS decrypt, Cognito administrative actions, and SQS operations. Split permissions by function and scope each action to the specific table, bucket/prefix, queue, user pool, key, or log group it needs.
- The deployment IAM user should not retain broad administrator policies for routine use. Use a dedicated, short-lived deployment role with only Terraform/Syndicate provisioning permissions, and separate runtime roles from deployment permissions.
- The EventBridge scheduler role is already scoped to invoking `report-sender`; the frontend S3 bucket blocks public access and grants reads only to the designated CloudFront distribution.

After narrowing permissions, redeploy and exercise representative application flows; verify CloudWatch logs for `AccessDenied` before removing any old policy. Do not edit the shared policy in place until each function's required AWS actions and resources have been mapped.

## Troubleshooting

### `InvalidBucketName`

S3 bucket names cannot contain braces or unresolved placeholders. In `infra/terraform/backend.tf`, replace values such as `restaurant-terraform-state-{ACCOUNT_ID}` with the real bucket name, for example `restaurant-terraform-state-123456789012`.

Check that:

- The bucket name contains only valid S3 bucket-name characters.
- The bucket name is globally unique.
- The bucket exists in the expected AWS account and region.
- No `{}`, `< >`, or other template placeholders remain in the configured value.

### `Required plugins are not installed`

Terraform may not have initialized the required provider plugins in the current working directory. Run:

```bash
cd infra/terraform
terraform init
terraform validate
```

If initialization still fails, check your internet connection, proxy/VPN settings, and access to the Terraform Registry, then retry `terraform init`.

### `no route to host` or other network errors

Check that your network connection and VPN are working and that your machine can reach AWS and the Terraform Registry. Once connectivity is restored, retry:

```bash
terraform init
```

### `Missing option '--deploy-target-bucket'`

Provide the deployment bucket when generating the Syndicate configuration:

```bash
syndicate generate config \
  --name dev \
  --region us-west-2 \
  --deploy-target-bucket "$DEPLOY_BUCKET"
```

Confirm that `DEPLOY_BUCKET` is set to a real, globally unique S3 bucket name:

```bash
printf '%s\n' "$DEPLOY_BUCKET"
```

### `Unresolved alias placeholders` or DynamoDB `ValidationException`

These errors mean one or more `${alias}` values in `deployment_resources.json` do not have corresponding entries in the `syndicate_aliases.yml` selected by `SDCT_CONF`. Confirm the active config directory and inspect the alias keys:

```bash
printf 'Syndicate config: %s\n' "$SDCT_CONF"
cat "$SDCT_CONF/syndicate_aliases.yml"
```

Every placeholder must have a matching key with a concrete value. For example, `${waiters_global_index}` must have `waiters_global_index: locationId-index`, and `${feedback_table_name}` must have `feedback_table_name: Feedback`. S3 bucket aliases must resolve to existing bucket names without `{}` or other placeholder characters.

For Cognito, `user_pool_name` must be the pool's **name**, not its ID, for example `restaurant-backend-user-pool`. Find the existing pool with:

```bash
aws cognito-idp list-user-pools \
  --region us-west-2 \
  --max-results 60 \
  --query "UserPools[?Name=='restaurant-backend-user-pool'].{Name:Name,Id:Id}" \
  --output table
```

Values inside Lambda `env_variables` in `lambda_config.json` are separate from deployment-resource aliases. Set those to literal table/index/queue/bucket names. After changing an existing Lambda's config, create a new bundle and run `syndicate update --update-only-types lambda`; a normal `syndicate deploy` may report the function already exists without updating its environment.

Validate a deployed Lambda environment and test the API:

```bash
aws lambda get-function-configuration \
  --function-name api-handler:dev \
  --region us-west-2 \
  --query 'Environment.Variables' \
  --output json

curl -i "https://${API_ID}.execute-api.us-west-2.amazonaws.com/develop/locations"
```

## Security and operational notes

- Keep AWS credentials out of the repository. Use an AWS CLI profile, environment-based credentials, or an approved credentials provider.
- Do not commit `.env` files containing private configuration or credentials.
- Limit deployment permissions to the resources and actions required by Terraform and Syndicate.
- Keep S3 public access blocked unless a resource explicitly requires public access; serve the frontend through the configured CloudFront distribution.
- Review Terraform plans and the target AWS account before applying infrastructure changes.
