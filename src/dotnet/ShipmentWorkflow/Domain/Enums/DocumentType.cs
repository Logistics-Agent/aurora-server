namespace ShipmentWorkflow.Domain.Enums;

public enum DocumentType
{
    Unknown = 0,
    Invoice = 1,
    PackingList = 2,
    BillOfLading = 3,
    CustomsDeclaration = 4,
    CertificateOfOrigin = 5,
    Other = 99
}
