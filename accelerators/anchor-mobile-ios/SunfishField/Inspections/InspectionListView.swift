import SwiftUI

// MARK: - InspectionListView

/// Lists scheduled + in-progress inspections fetched from Bridge.
///
/// Per W#23.3 Phase 1 hand-off. Pulls live from `GET /api/v1/field/inspections`.
/// Phase 3 adds GRDB offline cache + stale indicator when last cache is >24h old.
public struct InspectionListView: View {
    let pairingResult: PairingResult
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32

    @State private var inspections: [InspectionListItem] = []
    @State private var isLoading = false
    @State private var loadError: String?
    @State private var selectedItem: InspectionListItem?

    public init(
        pairingResult: PairingResult,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32
    ) {
        self.pairingResult = pairingResult
        self.queueService = queueService
        self.blobStore = blobStore
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
                list
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
                    Button {
                        Task { await loadInspections() }
                    } label: {
                        Image(systemName: "arrow.clockwise")
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
    }

    // MARK: Sub-views

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
            inspections = try await fetchFromBridge()
        } catch {
            loadError = error.localizedDescription
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
