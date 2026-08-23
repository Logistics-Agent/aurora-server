import { Injectable, ForbiddenException } from '@nestjs/common';
import { IAssistantTool, ToolExecutionContext, ToolResult } from './tool.interface';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ReadModelStore } from '../../read-model/read-model.store';

@Injectable()
export class ShipmentLookupTool implements IAssistantTool {
  readonly name = 'ShipmentLookupTool';
  readonly description = 'Looks up shipment statuses, locations, and routes scoped to authenticated actor role.';
  readonly supportedIntents = [AssistantIntent.SHIPMENT_QUERY];
  readonly allowedActors = [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN, ActorType.SYSTEM];

  constructor(private readonly readModel: ReadModelStore) {}

  async execute(context: ToolExecutionContext, params?: any): Promise<ToolResult> {
    const { actorType, customerId } = context.currentUser;

    if (!this.allowedActors.includes(actorType)) {
      throw new ForbiddenException(`Actor ${actorType} is not authorized to execute ${this.name}`);
    }

    const specificShipmentId = params?.shipmentId;

    // 1. Specific Shipment Lookup
    if (specificShipmentId) {
      const shipment = this.readModel.getShipment(specificShipmentId);
      if (shipment) {
        // Enforce customer boundary: Customer can only view their own shipment
        if (actorType === ActorType.CUSTOMER && shipment.customerId !== customerId) {
          return {
            toolName: this.name,
            success: false,
            data: null,
            summary: `Không tìm thấy hoặc quý khách không có quyền truy cập lô hàng ${specificShipmentId}.`,
          };
        }

        return {
          toolName: this.name,
          success: true,
          data: shipment,
          summary: `Lô hàng ${shipment.shipmentId} (${shipment.originPort} -> ${shipment.destinationPort}) hiện có trạng thái ${shipment.status}. Vị trí hiện tại: ${shipment.currentLocation || 'Đang vận chuyển'}.`,
        };
      }
    }

    // 2. Customer Scoped List
    if (actorType === ActorType.CUSTOMER) {
      const activeCustomerId = customerId || 'CUST-001';
      const shipments = this.readModel.getShipmentsByCustomer(activeCustomerId);
      if (shipments.length === 0) {
        return {
          toolName: this.name,
          success: true,
          data: [],
          summary: `Hiện tại không tìm thấy lô hàng nào đang hoạt động cho tài khoản quý khách.`,
        };
      }

      const primary = shipments[0];
      return {
        toolName: this.name,
        success: true,
        data: shipments,
        summary: `Quý khách có ${shipments.length} lô hàng. Lô gần nhất ${primary.shipmentId} (${primary.originPort} -> ${primary.destinationPort}) trạng thái: ${primary.status}. Vị trí: ${primary.currentLocation || 'Đang vận chuyển'}.`,
      };
    }

    // 3. Staff / Admin Tenant Scope List
    const targetCustId = params?.targetCustomerId || customerId || 'CUST-001';
    const shipments = this.readModel.getShipmentsByCustomer(targetCustId);
    if (shipments.length === 0) {
      return {
        toolName: this.name,
        success: true,
        data: [],
        summary: `Không tìm thấy lô hàng nào cho khách hàng ${targetCustId} trong hệ thống.`,
      };
    }

    const primary = shipments[0];
    return {
      toolName: this.name,
      success: true,
      data: shipments,
      summary: `[Staff View] Khách hàng ${targetCustId} có ${shipments.length} lô hàng. Lô ${primary.shipmentId} (${primary.originPort} -> ${primary.destinationPort}) trạng thái: ${primary.status}.`,
    };
  }
}
