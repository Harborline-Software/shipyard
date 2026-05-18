import Foundation

/// Outcome of a successful device-pairing handshake with Anchor via Bridge.
///
/// Written by `PairingService.pairAsync(code:)` (Phase 5) and persisted to
/// Keychain so the app can resume in the paired state after a restart.
///
/// **Keychain storage strategy:** a single JSON blob stored under the service
/// key `dev.sunfish.field.pairing-result` with `kSecAttrAccessibleAfterFirstUnlock`
/// — the same protection class used for the install Ed25519 keypair so both are
/// readable after first unlock, consistent with the background-session read
/// requirements from ADR 0028-A2.8.
public struct PairingResult: Codable, Equatable, Sendable {
    /// The tenant that paired this device.
    public let tenantId: String
    /// Base URL for the paired Anchor instance's Bridge endpoint.
    public let anchorBaseUrl: String
    /// Token expiry returned by the pairing handshake.
    public let expiresAt: Date

    public init(tenantId: String, anchorBaseUrl: String, expiresAt: Date) {
        self.tenantId = tenantId
        self.anchorBaseUrl = anchorBaseUrl
        self.expiresAt = expiresAt
    }

    // MARK: Keychain persistence

    private static let keychainService = "dev.sunfish.field.pairing-result"

    /// Persist this `PairingResult` to Keychain. Overwrites any existing entry.
    public func saveToKeychain() {
        guard let data = try? JSONEncoder().encode(self) else { return }
        let query: [CFString: Any] = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: Self.keychainService,
            kSecValueData: data,
            kSecAttrAccessible: kSecAttrAccessibleAfterFirstUnlock,
        ]
        SecItemDelete(query as CFDictionary)
        SecItemAdd(query as CFDictionary, nil)
    }

    /// Load a previously-persisted `PairingResult` from Keychain.
    /// Returns `nil` when no pairing has been established or the data is corrupt.
    public static func loadFromKeychain() -> PairingResult? {
        let query: [CFString: Any] = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: keychainService,
            kSecReturnData: kCFBooleanTrue!,
            kSecMatchLimit: kSecMatchLimitOne,
        ]
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status == errSecSuccess,
              let data = result as? Data,
              let pr = try? JSONDecoder().decode(PairingResult.self, from: data)
        else { return nil }
        return pr
    }

    /// Remove any stored `PairingResult` from Keychain.
    /// Called on unpair or factory-reset.
    public static func removeFromKeychain() {
        let query: [CFString: Any] = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: keychainService,
        ]
        SecItemDelete(query as CFDictionary)
    }
}
