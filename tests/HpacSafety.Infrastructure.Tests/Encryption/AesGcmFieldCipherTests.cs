using System.Security.Cryptography;

using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Encryption;

/// <summary>
/// The cipher that keeps reporter and pilot contact details out of the database
/// in plaintext. See ADR-0019 and <c>docs/data-handling.md</c>.
/// </summary>
public sealed class AesGcmFieldCipherTests
{
    private const string ContactDetail = "Vince Bergeron, 403-555-0134";

    private static AesGcmFieldCipher CipherWith(byte value) =>
        new(new FieldEncryptionOptions { Key = Convert.ToBase64String(Enumerable.Repeat(value, 32).ToArray()) });

    [Fact]
    public void Given_a_contact_field_When_it_is_encrypted_and_decrypted_Then_the_original_text_comes_back()
    {
        // Given
        var cipher = CipherWith(0x11);

        // When
        var roundTripped = cipher.Decrypt(cipher.Encrypt(ContactDetail));

        // Then
        roundTripped.ShouldBe(ContactDetail);
    }

    [Fact]
    public void Given_a_contact_field_When_it_is_encrypted_Then_the_ciphertext_does_not_contain_the_plaintext()
    {
        // Given
        var cipher = CipherWith(0x11);

        // When
        var ciphertext = cipher.Encrypt(ContactDetail);

        // Then
        ciphertext.ShouldNotContain("Vince");
        ciphertext.ShouldNotContain("403-555-0134");
    }

    [Fact]
    public void Given_the_same_text_encrypted_twice_When_the_two_ciphertexts_are_compared_Then_they_differ()
    {
        // Given
        var cipher = CipherWith(0x11);

        // When
        var first = cipher.Encrypt(ContactDetail);
        var second = cipher.Encrypt(ContactDetail);

        // Then — a fresh nonce every time, so equal plaintexts are not visibly equal on disk.
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Given_text_encrypted_under_one_key_When_it_is_decrypted_under_another_Then_it_cannot_be_read()
    {
        // Given
        var ciphertext = CipherWith(0x11).Encrypt(ContactDetail);
        var otherKey = CipherWith(0x22);

        // When / Then
        Should.Throw<FieldDecryptionException>(() => otherKey.Decrypt(ciphertext));
    }

    [Theory]
    [InlineData(0)]    // the nonce
    [InlineData(12)]   // the authentication tag
    [InlineData(28)]   // the ciphertext itself
    public void Given_a_ciphertext_altered_after_it_was_written_When_it_is_decrypted_Then_it_is_rejected(int offset)
    {
        // Given — flip one bit of the stored envelope. Editing the base64 text
        // is not enough: its last character carries unused bits, so a changed
        // character does not always change a decoded byte.
        var cipher = CipherWith(0x11);
        var envelope = Convert.FromBase64String(cipher.Encrypt(ContactDetail)["v1.".Length..]);
        envelope[offset] ^= 0x01;

        // When / Then
        Should.Throw<FieldDecryptionException>(() => cipher.Decrypt("v1." + Convert.ToBase64String(envelope)));
    }

    [Fact]
    public void Given_a_ciphertext_truncated_after_it_was_written_When_it_is_decrypted_Then_it_is_rejected()
    {
        // Given — shorter than a nonce and a tag together, so there is nothing
        // left to authenticate.
        var cipher = CipherWith(0x11);

        // When / Then
        Should.Throw<FieldDecryptionException>(
            () => cipher.Decrypt("v1." + Convert.ToBase64String(new byte[8])));
    }

    [Fact]
    public void Given_stored_text_that_is_not_base64_When_it_is_decrypted_Then_it_is_rejected()
    {
        // Given
        var cipher = CipherWith(0x11);

        // When / Then
        Should.Throw<FieldDecryptionException>(() => cipher.Decrypt("v1.not base64 at all!!"));
    }

    [Fact]
    public void Given_text_this_cipher_never_wrote_When_it_is_decrypted_Then_it_is_rejected()
    {
        // Given
        var cipher = CipherWith(0x11);

        // When / Then
        Should.Throw<FieldDecryptionException>(() => cipher.Decrypt("Vince Bergeron"));
    }

    [Fact]
    public void Given_a_failed_decryption_When_the_message_is_read_Then_it_carries_no_field_content()
    {
        // Given
        var cipher = CipherWith(0x11);
        var ciphertext = CipherWith(0x22).Encrypt(ContactDetail);

        // When
        var thrown = Should.Throw<FieldDecryptionException>(() => cipher.Decrypt(ciphertext));

        // Then
        thrown.Message.ShouldNotContain(ciphertext);
        thrown.Message.ShouldNotContain("Vince");
    }

    [Fact]
    public void Given_two_ciphers_holding_the_same_key_When_their_key_identifiers_are_compared_Then_they_match()
    {
        // Given
        var one = CipherWith(0x11);
        var another = CipherWith(0x11);

        // When / Then
        one.KeyId.ShouldBe(another.KeyId);
    }

    [Fact]
    public void Given_two_ciphers_holding_different_keys_When_their_key_identifiers_are_compared_Then_they_differ()
    {
        // Given
        var one = CipherWith(0x11);
        var another = CipherWith(0x22);

        // When / Then
        one.KeyId.ShouldNotBe(another.KeyId);
    }

    [Fact]
    public void Given_a_key_identifier_When_it_is_inspected_Then_it_is_not_the_key()
    {
        // Given
        var key = Convert.ToBase64String(Enumerable.Repeat((byte)0x11, 32).ToArray());

        // When
        var keyId = new AesGcmFieldCipher(new FieldEncryptionOptions { Key = key }).KeyId;

        // Then
        keyId.ShouldNotBe(key);
        keyId.Length.ShouldBeLessThan(key.Length);
    }

    [Fact]
    public void Given_no_configured_key_When_the_cipher_is_built_Then_it_refuses_to_start()
    {
        // Given
        var options = new FieldEncryptionOptions { Key = string.Empty };

        // When / Then
        Should.Throw<InvalidOperationException>(() => new AesGcmFieldCipher(options));
    }

    [Fact]
    public void Given_a_key_that_is_not_256_bits_When_the_cipher_is_built_Then_it_refuses_to_start()
    {
        // Given
        var options = new FieldEncryptionOptions { Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) };

        // When / Then
        Should.Throw<InvalidOperationException>(() => new AesGcmFieldCipher(options));
    }

    [Fact]
    public void Given_a_key_that_is_not_base64_When_the_cipher_is_built_Then_it_refuses_to_start()
    {
        // Given
        var options = new FieldEncryptionOptions { Key = "not base64 at all!!" };

        // When / Then
        Should.Throw<InvalidOperationException>(() => new AesGcmFieldCipher(options));
    }

    [Fact]
    public void Given_an_empty_string_When_it_is_encrypted_and_decrypted_Then_it_survives()
    {
        // Given
        var cipher = CipherWith(0x11);

        // When
        var roundTripped = cipher.Decrypt(cipher.Encrypt(string.Empty));

        // Then
        roundTripped.ShouldBe(string.Empty);
    }

    [Fact]
    public void Given_accented_French_text_When_it_is_encrypted_and_decrypted_Then_it_survives_unchanged()
    {
        // Given
        var cipher = CipherWith(0x11);
        const string text = "Éric Côté, décollage à Saint-André-d'Argenteuil";

        // When
        var roundTripped = cipher.Decrypt(cipher.Encrypt(text));

        // Then
        roundTripped.ShouldBe(text);
    }
}
