eal-Time Chat App (StateleSSE)

A real-time multi-room chat application built with ASP.NET Core, PostgreSQL, Redis, SSE and React (Vite + TypeScript).

The system supports role-based access, private messaging for admins, room management, and horizontal scalability using Redis.

-- Features
-- Rooms

Multiple chat rooms

Admins can:

Create rooms

Archive (soft-delete) rooms

Only active rooms are listed

-- Messaging

Public messages (visible to everyone in the room)

Real-time updates using Server-Sent Events (SSE)

Optimistic UI updates

-- Authentication & Authorization

JWT-based authentication

Password hashing using ASP.NET Core Identity

Role-based authorization

Admin-only features:

Create room

Archive room

Send private messages (DM)

-- Private Messages (DM)

Admin can send DMs to specific users

DMs are:

Delivered only to sender and recipient

Not broadcast to the room

Not visible to guests

-- Online Tracking

Live online user count per room

Based on active SSE group members

-- Soft Delete

Rooms are archived using IsArchived = true

Archived rooms are hidden but preserved in database

-- Technologies Used
Backend

ASP.NET Core

Entity Framework Core

PostgreSQL

Redis (Backplane for scaling SSE)

StateleSSE.AspNetCore

JWT Authentication

PasswordHasher (ASP.NET Identity)

Frontend

React

Vite

TypeScript

EventSource (SSE client)

--Architecture Overview

REST API for:

Authentication

Room management

Sending messages

SSE endpoints for:

Room message streaming

User-specific message streaming (DM)

Redis backplane enables:

Horizontal scaling

Multi-instance real-time delivery

--Security Highlights

JWT validation with issuer, audience, and signing key

Role-based endpoint protection

DM messages never broadcast to room groups

Guests cannot see private messages

Token accepted via access_token query for SSE connections

--Learning Focus

This project demonstrates:

Real-time communication without SignalR

SSE group management

JWT authentication with SSE

Role-based authorization

Clean service-layer architecture

Scalable real-time backend design

docker compose up -d (Redis local)

set REDIS_CONNECTION for cloud

dotnet run (server)

npm run dev (client, later)

endpoints: /swagger, /sse, /messages



FOR MIGRATION
PowerShell
$env:ASPNETCORE_ENVIRONMENT="Migration"
dotnet ef migrations add InitialCreate --project ./dataaccess --startup-project ./api
dotnet ef database update --project ./dataaccess --startup-project ./api

Git Bash (your terminal)
export ASPNETCORE_ENVIRONMENT=Migration
dotnet ef migrations add InitialCreate --project ./dataaccess --startup-project ./api
dotnet ef database update --project ./dataaccess --startup-project ./api


After you’re done, you can unset it:

unset ASPNETCORE_ENVIRONMENT

user: ali
password: test

user: admin
password: admin
Role: Admin

I deployed this project
frontend link:
https://frontend-chatappada.fly.dev/

back-end link:
https://backend-chatappada.fly.dev/