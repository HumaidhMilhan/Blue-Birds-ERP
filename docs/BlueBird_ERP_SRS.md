# PoultryPro ERP --- Software Requirements Specification (SRS) v1.0

**Version:** 1.0\
**Date:** April 2026\
**Classification:** Confidential\
**Platform:** Windows Desktop Application\
**Prepared For:** Poultry Shop Client\
**Document Type:** Software Requirements Specification (SRS)

------------------------------------------------------------------------

## 1. Introduction

### 1.1 Purpose

This Software Requirements Specification (SRS) document describes the
functional and non-functional requirements for the PoultryPro ERP system
--- a Windows desktop application designed for a pre-processed poultry
retail and wholesale business.

It serves as: - A contractual basis between the development team and the
client\
- The primary input for system design, development, and acceptance
testing

------------------------------------------------------------------------

### 1.2 Scope

The system includes:

-   Point-of-Sale (POS) for retail and wholesale\
-   Batch-based inventory management with wastage tracking\
-   Supplier purchase management with Goods Received Notes (GRN)\
-   Credit account management with soft-limit enforcement\
-   Sales and purchase return processing\
-   WhatsApp notifications via Twilio API\
-   Role-Based Access Control (RBAC)\
-   Partial offline capability

------------------------------------------------------------------------

### 1.3 Definitions

  Term      Definition
  --------- ------------------------------------------
  POS       Point of Sale system
  GRN       Goods Received Note
  RBAC      Role-Based Access Control
  FIFO      First In First Out
  Batch     Inventory unit tied to supplier delivery
  Wastage   Irrecoverable stock loss
  ERP       Enterprise Resource Planning

------------------------------------------------------------------------

### 1.4 Document Overview

-   Section 2: Overall Description\
-   Section 3: Functional Requirements\
-   Section 4: Non-Functional Requirements\
-   Section 5: Data Model

------------------------------------------------------------------------

## 2. Overall Description

### 2.1 Product Perspective

-   Standalone Windows desktop application\
-   Offline POS using SQLite\
-   Sync with central DB (PostgreSQL/MySQL)\
-   WhatsApp integration via Twilio

------------------------------------------------------------------------

### 2.2 User Classes

  Role         Technical Level   Responsibilities
  ------------ ----------------- ---------------------------
  Admin        Moderate          Full system control
  Cashier      Low               POS operations
  Shop Owner   Low               Receives WhatsApp reports

------------------------------------------------------------------------

### 2.3 Operating Environment

-   Windows 10 / 11\
-   SQLite (local)\
-   PostgreSQL/MySQL (server)\
-   LAN + Internet\
-   Thermal printer support

------------------------------------------------------------------------

### 2.4 Assumptions

-   Pre-processed poultry only\
-   Twilio account configured\
-   Customers have WhatsApp numbers\
-   Pricing controlled by Admin\
-   Offline sync resolves without conflict

------------------------------------------------------------------------

## 3. Functional Requirements

### 3.1 POS & Sales

-   Retail & Wholesale channels\
-   Payment types:
    -   Retail: Cash, Card\
    -   Wholesale: Cash, Card, Credit, Mixed\
-   Batch selection per item\
-   Invoice generation with full details\
-   Credit due date auto-calculation\
-   Partial payments supported\
-   Admin-only invoice void with audit log

------------------------------------------------------------------------

### 3.2 Customer Management

-   Business Accounts with credit settings\
-   One-Time Creditor accounts\
-   Real-time credit tracking\
-   Payment recording & allocation\
-   Debtor aging reports

------------------------------------------------------------------------

### 3.3 Inventory Management

-   Product catalog (weight & unit based)\
-   Batch tracking with expiry\
-   Real-time stock visibility\
-   Manual batch selection at POS\
-   Wastage tracking\
-   No restocking for customer returns\
-   Low-stock & near-expiry alerts

------------------------------------------------------------------------

### 3.4 Purchase & Returns

-   Supplier management\
-   Purchase Orders (PO)\
-   Goods Received Notes (GRN)\
-   Batch creation on receipt\
-   Purchase returns handling\
-   Sales returns create wastage records

------------------------------------------------------------------------

### 3.5 WhatsApp Notifications

-   Payment reminders\
-   Overdue alerts\
-   Daily sales summary\
-   Configurable templates\
-   Retry mechanism\
-   Notification logging

------------------------------------------------------------------------

### 3.6 RBAC

-   Roles: Admin, Cashier\
-   Restricted access for Cashier\
-   Full access for Admin\
-   Audit logging for all actions\
-   Session timeout enforcement

------------------------------------------------------------------------

## 4. Non-Functional Requirements

  ID       Category          Requirement
  -------- ----------------- ------------------------------
  NFR-1    Performance       POS \< 2s, reports \< 5s
  NFR-2    Offline           POS works without internet
  NFR-3    Security          bcrypt, AES-256, TLS 1.2+
  NFR-4    Data Integrity    No negative stock
  NFR-5    Usability         Fast checkout flow
  NFR-6    Auditability      Immutable logs
  NFR-7    Scalability       10,000 invoices/month
  NFR-8    Configurability   Admin configurable settings
  NFR-9    Platform          Windows 10/11
  NFR-10   Reliability       Retry failed notifications
  NFR-11   Maintainability   Config without recompilation

------------------------------------------------------------------------

## 5. Data Model

### Key Entities

-   User\
-   Customer\
-   BusinessAccount\
-   Supplier\
-   Product\
-   Batch\
-   Invoice\
-   InvoiceItem\
-   Payment\
-   WastageRecord\
-   SalesReturn\
-   Notification\
-   AuditLog

------------------------------------------------------------------------

## Appendix A --- Revision History

  Version   Date         Description
  --------- ------------ ---------------
  1.0       April 2026   Initial Draft

------------------------------------------------------------------------

## Appendix B --- Out of Scope

-   Multi-branch support\
-   Live poultry handling\
-   Accounting systems\
-   E-commerce integration\
-   Mobile apps\
-   Barcode scanning (future version)
