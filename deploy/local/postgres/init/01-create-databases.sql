-- =============================================================================
-- Aurora Local Database Initialization Script
-- =============================================================================
-- Pre-creates all databases required by Aurora microservices.
-- =============================================================================

CREATE DATABASE iam_tenant_db;
CREATE DATABASE shipment_workflow_db;
CREATE DATABASE gps_tracking_db;
CREATE DATABASE billing_db;
CREATE DATABASE financial_db;
CREATE DATABASE notification_db;
CREATE DATABASE audit_service_db;
CREATE DATABASE mail_service_db;
CREATE DATABASE ai_governance_db;
CREATE DATABASE devops_agent_db;
CREATE DATABASE customer_assistant_db;
CREATE DATABASE negotiation_agent_db;
CREATE DATABASE document_ocr_db;
CREATE DATABASE route_planning_db;

\c iam_tenant_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c shipment_workflow_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c gps_tracking_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c mail_service_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
