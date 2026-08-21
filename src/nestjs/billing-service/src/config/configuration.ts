export default () => ({
  environment: process.env.NODE_ENV || 'development',
  database: {
    url: process.env.DATABASE_URL,
    directUrl: process.env.DIRECT_URL,
  },
  grpc: {
    host: process.env.GRPC_HOST || '0.0.0.0',
    port: parseInt(process.env.GRPC_PORT, 10) || 5004,
    financialServiceUrl: process.env.FINANCIAL_SERVICE_GRPC_URL || 'localhost:5003',
  },
  billing: {
    defaultPaymentTermsDays: parseInt(process.env.DEFAULT_PAYMENT_TERMS_DAYS, 10) || 30,
    s3BucketName: process.env.S3_BUCKET_NAME || 'aurora-private-docs',
    defaultCurrency: process.env.DEFAULT_CURRENCY || 'USD',
  },
});
