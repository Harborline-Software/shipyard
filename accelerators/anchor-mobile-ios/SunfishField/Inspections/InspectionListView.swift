import SwiftUI

// MARK: - InspectionListView

/// Lists scheduled + in-progress inspections fetched from Bridge.
///
/// Per W#23.3 Phase 1 hand-off. Pulls live from `GET /api/v1/field/inspections`.
/// Phase 3: caches results in GRDB; shows offline banner + stale warning when
/// the cached data is more than 24h old.
public struct InspectionListView: View {
    let pairingResult: PairingResult
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let database: AppDatabase?
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32

    @State private var inspections: [InspectionListItem] = []
    @State private var isLoading = false
    @State private var loadError: String?
    @State private var selectedItem: InspectionListItem?
    @State private var isOffline = false
    @State private var cacheAge: TimeInterval?
    @State private var showNewSheet = false

    private var isStale: Bool {
        guard let age = cacheAge else { return false }
        return age > 24 * 3600
    }

    public init(
        pairingResult: PairingResult,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        database: AppDatabase? = nil,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32
    ) {
        self.pairingResult = pairingResult
        self.queueService = queueService
        self.blobStore = blobStore
        self.database = database
        self.deviceId = deviceId
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
    }

    public var body: some View {
        Group {
            if isLoading && inspections.isEmpty {
                ProgressView("Loading inspections…")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let err = loadError, inspections.isEmpty {
                errorView(message: err)
            } else if inspections.isEmpty {
                emptyView
            } else {
                VStack(spacing: 0) {
                    if isOffline || isStale {
                        offlineBanner
                    }
                    list
                }
            }
        }
        .navigationTitle("Inspections")
        #if os(iOS)
        .navigationBarTitleDisplayMode(.large)
        #endif
        .toolbar {
            ToolbarItem(placement: {
                #if os(iOS)
                return .topBarTrailing
                #else
                return .automatic
                #endif
            }()) {
                if isLoading {
                    ProgressView().scaleEffect(0.75)
                } else {
                    HStack(spacing: 12) {
                        Button {
                            showNewSheet = true
                        } label: {
                            Image(systemName: "plus")
                        }
                        Button {
                            Task { await loadInspections() }
                        } label: {
                            Image(systemName: "arrow.clockwise")
                        }
                    }
                }
            }
        }
        .task { await loadInspections() }
        .sheet(item: $selectedItem) { item in
            InspectionDetailView(
                item: item,
                queueService: queueService,
                blobStore: blobStore,
                deviceId: deviceId,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch,
                onEventQueued: {
                    Task { await loadInspections() }
                }
            )
        }
        .sheet(isPresented: $showNewSheet) {
            NewInspectionSheet(
                queueService: queueService,
                deviceId: deviceId,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
        }
    }

    // MARK: Sub-views

    private var offlineBanner: some View {
        HStack(spacing: 6) {
            Image(systemName: isStale ? "exclamationmark.triangle.fill" : "wifi.slash")
                .font(.caption)
            Text(isStale ? "Cached data is over 24h old" : "Showing cached inspections")
                .font(.caption)
        }
        .foregroundStyle(isStale ? .orange : .secondary)
        .frame(maxWidth: .infinity)
        .padding(.vertical, 6)
        .background(
            isStale
                ? Color.orange.opacity(0.12)
                : Color.gray.opacity(0.08)
        )
    }

    private var list: some View {
        List(inspections) { item in
            InspectionRowView(item: item)
                .contentShape(Rectangle())
                .onTapGesture { selectedItem = item }
                .listRowInsets(EdgeInsets(top: 8, leading: 16, bottom: 8, trailing: 16))
        }
        .listStyle(.plain)
        .refreshable { await loadInspections() }
    }

    private var emptyView: some View {
        VStack(spacing: 12) {
            Image(systemName: "checklist")
                .font(.largeTitle)
                .foregroundStyle(.secondary)
            Text("No inspections scheduled")
                .font(.headline)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func errorView(message: String) -> some View {
        VStack(spacing: 12) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.largeTitle)
                .foregroundStyle(.yellow)
            Text("Could not load inspections")
                .font(.headline)
            Text(message)
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            Button("Try Again") {
                Task { await loadInspections() }
            }
            .buttonStyle(.borderedProminent)
        }
        .padding(.horizontal, 32)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: Data loading

    private func loadInspections() async {
        isLoading = true
        loadError = nil
        defer { isLoading = false }
        do {
            let fresh = try await fetchFromBridge()
            inspections = fresh
            isOffline = false
            cacheAge = 0
            if let db = database {
                try? db.cacheInspections(fresh)
            }
        } catch {
            // Network failed — fall back to GRDB cache if available
            if let db = database,
               let cached = try? db.cachedInspections(),
               !cached.isEmpty {
                inspections = cached.map { $0.item }
                isOffline = true
                cacheAge = cached.first.map { Date().timeIntervalSince($0.cachedAt) }
            } else {
                loadError = error.localizedDescription
            }
        }
    }

    private func fetchFromBridge() async throws -> [InspectionListItem] {
        let base = pairingResult.anchorBaseUrl.hasSuffix("/")
            ? String(pairingResult.anchorBaseUrl.dropLast())
            : pairingResult.anchorBaseUrl
        guard let url = URL(string: "\(base)/api/v1/field/inspections") else {
            throw URLError(.badURL)
        }
        let (data, _) = try await URLSession.shared.data(from: url)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode([InspectionListItem].self, from: data)
    }
}

// MARK: - InspectionRowView

private struct InspectionRowView: View {
    let item: InspectionListItem

    var body: some View {
        HStack(spacing: 12) {
            phaseChip
            VStack(alignment: .leading, spacing: 4) {
                Text(item.templateName ?? "Inspection")
                    .font(.body.weight(.medium))
                Text("ID: \(item.propertyId)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                if let scheduled = item.scheduledFor {
                    Text(scheduled)
                        .font(.caption2)
                        .foregroundStyle(.tertiary)
                }
            }
            Spacer()
            if item.totalItems > 0 {
                Text("\(item.respondedItems)/\(item.totalItems)")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }
            Image(systemName: "chevron.right")
                .font(.caption)
                .foregroundStyle(.quaternary)
        }
    }

    private var phaseChip: some View {
        let phase = InspectionPhaseLocal(rawValue: item.phase)
        return Text(phase?.displayName ?? item.phase)
            .font(.caption2.weight(.semibold))
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(
                RoundedRectangle(cornerRadius: 6)
                    .fill(phase == .inProgress ? Color.orange.opacity(0.15) : Color.blue.opacity(0.12))
            )
            .foregroundStyle(phase == .inProgress ? .orange : .blue)
    }
}
