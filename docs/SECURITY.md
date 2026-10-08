# BranchScribe Security Considerations

## Local-First Data Handling

BranchScribe processes all sensitive customer data locally by default:

- **No data is sent to cloud services** unless the routing evaluator explicitly decides cloud processing is appropriate AND no PII is detected.
- **Transcript data stays on-device** and is processed by local AI models.
- **Form extraction and field data** remain in local memory during the session.

## Local Storage Encryption

> **Status: Placeholder**

For production deployment, implement the following:

- [ ] Encrypt session data at rest using Windows DPAPI or similar OS-level encryption.
- [ ] Use `Windows.Security.Credentials.PasswordVault` for storing any API keys or tokens.
- [ ] Implement secure deletion of session data after configurable retention period.
- [ ] Encrypt the local SQLite database (if used) with SQLCipher or similar.

### Recommended Implementation

```csharp
// TODO: Implement ISecureStorage interface
public interface ISecureStorage
{
    Task<byte[]> EncryptAsync(byte[] data);
    Task<byte[]> DecryptAsync(byte[] encryptedData);
    Task StoreSecretAsync(string key, string value);
    Task<string?> GetSecretAsync(string key);
}
```

## Consent UI

> **Status: Placeholder**

Before collecting or processing customer data, the application should:

- [ ] Display a consent dialog explaining what data is collected and how it's processed.
- [ ] Record consent with timestamp and advisor ID.
- [ ] Allow customers to withdraw consent and delete their data.
- [ ] Provide a data export mechanism (GDPR/DSAR compliance).

### Recommended Consent Flow

1. Advisor starts a new session.
2. App displays consent dialog to be shown/read to customer.
3. Customer verbally consents (advisor clicks "Consent Given").
4. Consent is recorded in session metadata.
5. If consent is declined, session operates in "limited mode" (no transcription).

## Content Moderation

The `ILocalLLM` interface includes a `ModerationResult` on each response:

- PhiSilicaLocalLLM includes basic PII detection (long digit strings).
- Production should integrate Windows AI content safety APIs.
- All LLM outputs should be reviewed by the advisor before sharing with customers.

## API Key Management

- Foundry Local API key (if any) is read from `appsettings.json`.
- **Do NOT commit real API keys to source control.**
- Use environment variables or Azure Key Vault for production deployments.
- The cloud routing stub does not use real keys.

## Network Security

- Foundry Local communicates over localhost only (`http://localhost:5273`).
- Cloud endpoint (when implemented) should use HTTPS with certificate pinning.
- Network requests should include appropriate timeouts and retry policies.
