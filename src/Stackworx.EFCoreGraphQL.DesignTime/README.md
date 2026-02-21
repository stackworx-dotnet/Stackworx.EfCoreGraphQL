# Readme

Sample project to test the DesignTime integration

Delete the existing migrations first or test update

```sh
dotnet tool restore
export STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR='./Migrations'
dotnet ef migrations add InitialCreate
```