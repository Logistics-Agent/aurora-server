export default () => ({
  environment: process.env.NODE_ENV || 'development',
  database: {
    url: process.env.DATABASE_URL,
  },
  grpc: {
    host: process.env.GRPC_HOST || '0.0.0.0',
    port: parseInt(process.env.GRPC_PORT, 10) || 5003,
  },
  logistics: {
    volumetricDivisorAir: parseFloat(process.env.VOLUMETRIC_DIVISOR_AIR) || 5000,
    volumetricDivisorSea: parseFloat(process.env.VOLUMETRIC_DIVISOR_SEA) || 6000,
    defaultVatRate: parseFloat(process.env.DEFAULT_VAT_RATE) || 10.0,
  },
});
