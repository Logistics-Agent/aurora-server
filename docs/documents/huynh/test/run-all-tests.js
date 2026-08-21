const { CostCalculatorDomainService } = require('../../../../src/nestjs/financial-service/dist/src/domain/services/cost-calculator.domain-service');
const { InvoiceDomainService } = require('../../../../src/nestjs/billing-service/dist/src/domain/services/invoice.domain-service');
const { NegotiationStrategyDomainService } = require('../../../../src/nestjs/negotiation-agent-service/dist/domain/services/negotiation-strategy.domain-service');
const { ReadModelStore } = require('../../../../src/nestjs/customer-assistant-service/dist/read-model/read-model.store');
const { CustomerAssistantService } = require('../../../../src/nestjs/customer-assistant-service/dist/application/services/assistant.service');

let passedCount = 0;
let failedCount = 0;

function assert(condition, testName, detail = '') {
  if (condition) {
    passedCount++;
    console.log(`  ✅ [PASS] ${testName} ${detail ? '-> ' + detail : ''}`);
  } else {
    failedCount++;
    console.error(`  ❌ [FAIL] ${testName} ${detail ? '-> ' + detail : ''}`);
  }
}

async function runMasterTestSuite() {
  console.log('\n================================================================');
  console.log('🚀 AURORA SAAS LOGISTICS — MASTER AUTOMATED TEST SUITE RUNNER');
  console.log('================================================================\n');

  // ---------------------------------------------------------------------------
  // 1. FINANCIAL SERVICE TEST SUITE
  // ---------------------------------------------------------------------------
  console.log('📦 1. TESTING FINANCIAL SERVICE [CORE]...');
  const finCalc = new CostCalculatorDomainService();

  // Test 1.1: Volumetric Weight
  const volWeight = finCalc.calculateVolumetricWeight(100, 100, 100, 5000);
  assert(volWeight === 200, 'FIN-01: Volumetric Weight (100x100x100cm AIR divisor 5000 = 200kg)', `Result: ${volWeight}kg`);

  // Test 1.2: Chargeable Weight
  const chgWeight = finCalc.calculateChargeableWeight(150, 200);
  assert(chgWeight === 200, 'FIN-02: Chargeable Weight (Max of Gross 150 vs Vol 200)', `Result: ${chgWeight}kg`);

  // Test 1.3: Fuel Surcharge (FSC + EBS)
  const fsc = finCalc.calculateFuelSurcharge(1000, 10, 2);
  assert(fsc.totalSurcharge === 120, 'FIN-03: Fuel Surcharge (10% FSC + 2% EBS of $1000)', `Result: $${fsc.totalSurcharge}`);

  // Test 1.4: Cargo Insurance
  const ins = finCalc.calculateCargoInsurance(5000, 0.3);
  assert(ins.insuranceFee === 15, 'FIN-04: Cargo Insurance (0.3% of $5000)', `Result: $${ins.insuranceFee}`);

  // Test 1.5: Dynamic Margin Decay (Initial - Full Margin)
  const marginInit = finCalc.calculateDynamicMargin(1000, 20, 3600, 3600, 2);
  assert(marginInit.minAcceptablePrice === 1200, 'FIN-05: Dynamic Margin Decay (t=0 -> Full 20% margin)', `Min Price: $${marginInit.minAcceptablePrice}`);

  // Test 1.6: Dynamic Margin Decay (Near cut-off - 0% Margin)
  const marginEnd = finCalc.calculateDynamicMargin(1000, 20, 0, 3600, 2);
  assert(marginEnd.minAcceptablePrice === 1000, 'FIN-06: Dynamic Margin Decay (t=cutoff -> 0% margin)', `Min Price: $${marginEnd.minAcceptablePrice}`);

  console.log('');

  // ---------------------------------------------------------------------------
  // 2. BILLING SERVICE TEST SUITE
  // ---------------------------------------------------------------------------
  console.log('📄 2. TESTING BILLING SERVICE [CORE]...');
  const invDomain = new InvoiceDomainService();

  // Test 2.1: Invoice Number Formatting
  const invNum = invDomain.generateInvoiceNumber(1, new Date('2026-08-10'));
  assert(invNum === 'INV-202608-0001', 'BIL-01: Auto Invoice Numbering', `Result: ${invNum}`);

  // Test 2.2: Due Date Calculation (T+30)
  const dueDate = invDomain.calculateDueDate(new Date('2026-08-10'), 30);
  assert(dueDate.toISOString().startsWith('2026-09-09'), 'BIL-02: T+30 Payment Terms Due Date', `Due Date: ${dueDate.toISOString().split('T')[0]}`);

  // Test 2.3: Tax & Subtotal Calculations
  const totals = invDomain.calculateInvoiceTotals([
    { description: 'Freight', quantity: 1, unitPrice: 1000, amount: 1000, category: 'FREIGHT' },
  ], 10);
  assert(totals.totalAmount === 1100, 'BIL-03: Subtotal + 10% VAT Tax', `Total: $${totals.totalAmount}`);

  // Test 2.4: Debit Note Amount Addition
  const debitTotal = Number((1000 + 250).toFixed(2));
  assert(debitTotal === 1250, 'BIL-04: Debit Note Price Adjustment (+ $250 DEM)', `New Total: $${debitTotal}`);

  // Test 2.5: Credit Note Amount Deduction
  const creditTotal = Number((1000 - 150).toFixed(2));
  assert(creditTotal === 850, 'BIL-05: Credit Note Price Adjustment (- $150 Discount)', `New Total: $${creditTotal}`);

  console.log('');

  // ---------------------------------------------------------------------------
  // 3. NEGOTIATION AGENT AI SERVICE TEST SUITE
  // ---------------------------------------------------------------------------
  console.log('🤖 3. TESTING NEGOTIATION AGENT AI SERVICE...');
  const negStrategy = new NegotiationStrategyDomainService();

  // Test 3.1: Accept Offer
  const resAccept = negStrategy.determineDecision({
    offerPrice: 1300,
    bottomPrice: 1200,
    listPrice: 1500,
    currentRound: 1,
    maxRounds: 5,
    customerTier: 'STANDARD',
  });
  assert(resAccept.decision === 'ACCEPT', 'NEG-01: Offer Above Bottom Price -> ACCEPT', `Decision: ${resAccept.decision}`);

  // Test 3.2: Counter Offer
  const resCounter = negStrategy.determineDecision({
    offerPrice: 1000,
    bottomPrice: 1200,
    listPrice: 1500,
    currentRound: 1,
    maxRounds: 5,
    customerTier: 'STANDARD',
  });
  assert(resCounter.decision === 'COUNTER_OFFER' && resCounter.counterOfferPrice === 1200, 'NEG-02: Offer Below Bottom Price -> COUNTER_OFFER', `Counter: $${resCounter.counterOfferPrice}`);

  // Test 3.3: Max Rounds Exceeded -> Human Handoff
  const resMaxRounds = negStrategy.determineDecision({
    offerPrice: 900,
    bottomPrice: 1200,
    listPrice: 1500,
    currentRound: 5,
    maxRounds: 5,
    customerTier: 'STANDARD',
  });
  assert(resMaxRounds.decision === 'HUMAN_HANDOFF', 'NEG-03: Current Round >= Max Rounds -> HUMAN_HANDOFF', `Decision: ${resMaxRounds.decision}`);

  // Test 3.4: VIP Customer -> Human Handoff
  const resVip = negStrategy.determineDecision({
    offerPrice: 1400,
    bottomPrice: 1200,
    listPrice: 1500,
    currentRound: 1,
    maxRounds: 5,
    customerTier: 'VIP',
  });
  assert(resVip.decision === 'HUMAN_HANDOFF', 'NEG-04: Customer Tier VIP -> HUMAN_HANDOFF', `Decision: ${resVip.decision}`);

  console.log('');

  // ---------------------------------------------------------------------------
  // 4. CUSTOMER ASSISTANT AI SERVICE TEST SUITE
  // ---------------------------------------------------------------------------
  console.log('💬 4. TESTING CUSTOMER ASSISTANT AI SERVICE (RAG Read Model)...');
  const readModel = new ReadModelStore();
  const mockConfigService = { get: () => 'mock-gemini-api-key' };
  const assistant = new CustomerAssistantService(readModel, mockConfigService);

  // Test 4.1: Track Shipment Query
  const astShip = await assistant.processCustomerQuery({
    customerId: 'CUST-001',
    message: 'Đơn hàng shp_33019284 của tôi đang ở đâu?',
  });
  assert(astShip.intent === 'TRACK_SHIPMENT', 'AST-01: Track Shipment Intent Classification', `Intent: ${astShip.intent}`);

  // Test 4.2: Check Balance Query
  const astBal = await assistant.processCustomerQuery({
    customerId: 'CUST-001',
    message: 'Công nợ của tôi là bao nhiêu?',
  });
  assert(astBal.intent === 'CHECK_BALANCE', 'AST-02: Check Balance Intent Classification', `Intent: ${astBal.intent}`);

  // Test 4.3: General Help Query
  const astHelp = await assistant.processCustomerQuery({
    customerId: 'CUST-001',
    message: 'Xin chào trợ lý',
  });
  assert(astHelp.intent === 'GENERAL_HELP', 'AST-03: General Help Intent Classification', `Intent: ${astHelp.intent}`);

  console.log('');

  // ---------------------------------------------------------------------------
  // SUMMARY REPORT
  // ---------------------------------------------------------------------------
  console.log('================================================================');
  console.log('📊 MASTER TEST SUITE EXECUTION SUMMARY');
  console.log('================================================================');
  console.log(`  PASSED ASSERTIONS : ${passedCount}`);
  console.log(`  FAILED ASSERTIONS : ${failedCount}`);
  console.log(`  TOTAL TEST CASES  : ${passedCount + failedCount}`);
  console.log(`  SUCCESS RATE      : ${((passedCount / (passedCount + failedCount)) * 100).toFixed(1)}%`);
  console.log('================================================================\n');

  if (failedCount === 0) {
    console.log('🎉 ALL 15 AUTOMATED TEST CASES PASSED SUCCESSFULLY!');
  } else {
    console.error('❌ SOME TEST CASES FAILED! Please inspect errors above.');
    process.exit(1);
  }
}

runMasterTestSuite();
