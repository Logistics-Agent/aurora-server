# API.Gateway Deployment & Environment Configuration

YARP Reverse Proxy routing traffic from Azure Application Gateway to `Staff.Bff`, `Admin.Bff`, and `System.Bff`.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ReverseProxy__Clusters__staff-bff-cluster__Destinations__destination1__Address` | Yes | No | `.env.local` | ConfigMap | `http://staff-bff:8080` |
| `ReverseProxy__Clusters__admin-bff-cluster__Destinations__destination1__Address` | Yes | No | `.env.local` | ConfigMap | `http://admin-bff:8080` |
| `ReverseProxy__Clusters__system-bff-cluster__Destinations__destination1__Address` | Yes | No | `.env.local` | ConfigMap | `http://system-bff:8080` |
