declare enum Environment {
    Development = "development",
    Production = "production",
    Test = "test"
}
export declare class EnvironmentVariables {
    NODE_ENV: Environment;
    DATABASE_URL: string;
    DIRECT_URL?: string;
    GRPC_PORT: number;
    GRPC_HOST: string;
    FINANCIAL_SERVICE_GRPC_URL: string;
    DEFAULT_PAYMENT_TERMS_DAYS: number;
    S3_BUCKET_NAME: string;
    DEFAULT_CURRENCY: string;
}
export declare function validate(config: Record<string, unknown>): EnvironmentVariables;
export {};
