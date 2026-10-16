# Billing migrations

`20260925000100_baseline` describes the tables that existed before Prisma migrations were introduced.
`20260926000100_commercial_authority` adds approved shipment prices and customer credit accounts.

For a **new empty** Billing database, run `npx prisma migrate deploy` before starting the service.

For an **existing** Billing database previously created with `prisma db push`, first confirm
that its tables match the baseline schema. Then mark only the baseline as applied:

```text
npx prisma migrate resolve --applied 20260925000100_baseline
npx prisma migrate deploy
```

Run these commands in a deployment migration job with `DATABASE_URL` and `DIRECT_URL` set.
Do not run `migrate resolve` on an empty database. Do not run `db push` against production.
