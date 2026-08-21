export default () => ({
  environment: process.env.NODE_ENV || 'development',
  websocket: {
    port: parseInt(process.env.WEBSOCKET_PORT, 10) || 5005,
    corsOrigin: process.env.CORS_ORIGIN || '*',
  },
  redis: {
    host: process.env.REDIS_HOST || 'localhost',
    port: parseInt(process.env.REDIS_PORT, 10) || 6379,
    password: process.env.REDIS_PASSWORD || undefined,
  },
  rabbitmq: {
    uri: process.env.RABBITMQ_URI || 'amqp://guest:guest@localhost:5672',
  },
  auth: {
    jwtSecret: process.env.JWT_SECRET || 'aurora_super_secret_jwt_key_2026',
  },
});
