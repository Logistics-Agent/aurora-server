declare const _default: () => {
    environment: string;
    database: {
        url: string;
        directUrl: string;
    };
    grpc: {
        host: string;
        port: number;
        financialServiceUrl: string;
    };
    billing: {
        defaultPaymentTermsDays: number;
        s3BucketName: string;
        defaultCurrency: string;
    };
};
export default _default;
