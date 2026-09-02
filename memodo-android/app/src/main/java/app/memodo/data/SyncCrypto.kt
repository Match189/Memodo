package app.memodo.data

import android.util.Base64
import org.json.JSONObject
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

/**
 * 同步载荷端到端加密（E2EE）：与 Windows 端 SyncCrypto.cs 严格同构。
 * 格式 V1：base64("MEMODO1" + salt[16] + nonce[12] + AES-256-GCM(ciphertext+tag))
 * 密钥派生 PBKDF2-HMAC-SHA256（210k 迭代）。口令不进同步协议、不落云端。
 * 口令为空 = 不加密（明文快照，向后兼容旧数据）。
 */
object SyncCrypto {
    private const val MAGIC = "MEMODO1"
    private const val SALT_LEN = 16
    private const val NONCE_LEN = 12
    private const val KEY_LEN = 256 // bits
    private const val ITERATIONS = 210_000
    private const val TAG_BITS = 128
    private val random = SecureRandom()

    /** 加密明文 JSON；口令为空返回原文（明文模式）。 */
    fun encrypt(plainJson: String, passphrase: String): String {
        if (passphrase.isEmpty()) return plainJson
        val salt = ByteArray(SALT_LEN).also(random::nextBytes)
        val nonce = ByteArray(NONCE_LEN).also(random::nextBytes)
        val key = deriveKey(passphrase, salt)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply {
            init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(TAG_BITS, nonce))
        }
        val ct = cipher.doFinal(plainJson.toByteArray(Charsets.UTF_8)) // 含 GCM tag（尾 16 字节）
        val out = ByteArray(MAGIC.length + SALT_LEN + NONCE_LEN + ct.size)
        MAGIC.toByteArray(Charsets.US_ASCII).copyInto(out)
        salt.copyInto(out, MAGIC.length)
        nonce.copyInto(out, MAGIC.length + SALT_LEN)
        ct.copyInto(out, MAGIC.length + SALT_LEN + NONCE_LEN)
        return Base64.encodeToString(out, Base64.NO_WRAP)
    }

    /**
     * 解密；非本格式（明文快照/旧数据）原样返回。
     * 口令错误（GCM 校验失败）返回 null。
     */
    fun tryDecrypt(payload: String, passphrase: String): String? {
        if (passphrase.isEmpty()) return payload
        val data = try {
            Base64.decode(payload.trim(), Base64.NO_WRAP)
        } catch (e: Exception) {
            return payload // 不是 base64 → 明文快照
        }
        val magic = MAGIC.toByteArray(Charsets.US_ASCII)
        if (data.size < magic.size + SALT_LEN + NONCE_LEN + 16 ||
            !data.copyOfRange(0, magic.size).contentEquals(magic)
        ) return payload // 无魔数 → 明文快照（口令刚启用时远端仍是旧明文）
        return try {
            val salt = data.copyOfRange(magic.size, magic.size + SALT_LEN)
            val nonce = data.copyOfRange(magic.size + SALT_LEN, magic.size + SALT_LEN + NONCE_LEN)
            val ct = data.copyOfRange(magic.size + SALT_LEN + NONCE_LEN, data.size)
            val key = deriveKey(passphrase, salt)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply {
                init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(TAG_BITS, nonce))
            }
            String(cipher.doFinal(ct), Charsets.UTF_8)
        } catch (e: Exception) {
            null // 口令错误（GCM tag 校验失败）
        }
    }

    /** 载荷是否为本加密格式（用于跳过明文行）。注意 payload 是 base64 文本，
     *  必须解码后比对魔数字节——直接对文本 startsWith("MEMODO1") 永远为 false
     *  （base64("MEMODO1…") 开头是 "TUVNT0RP"）。 */
    fun isEncrypted(payload: String): Boolean = try {
        val d = Base64.decode(payload.trim(), Base64.NO_WRAP)
        val m = MAGIC.toByteArray(Charsets.US_ASCII)
        d.size >= m.size + SALT_LEN + NONCE_LEN + 16 && d.copyOfRange(0, m.size).contentEquals(m)
    } catch (e: Exception) { false }

    private fun deriveKey(passphrase: String, salt: ByteArray): ByteArray =
        SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
            .generateSecret(PBEKeySpec(passphrase.toCharArray(), salt, ITERATIONS, KEY_LEN))
            .encoded

    /** 口令指纹（SHA-256 hex）：客户端据此检测口令变化并触发全量重拉。空口令也有指纹（=清除态）。 */
    fun fingerprint(passphrase: String): String {
        val md = java.security.MessageDigest.getInstance("SHA-256")
        return md.digest(passphrase.toByteArray(Charsets.UTF_8))
            .joinToString("") { "%02x".format(it) }
    }

    // ---- 口令存储：主密钥 AES-GCM 加密后落 SharedPreferences（明文口令不出现在磁盘） ----
    private const val PREFS = "sync_settings"
    private const val KEY_MAIN = "e2ee_main_key"     // 随机主密钥（AES-256）
    private const val KEY_PASS_ENC = "e2ee_pass_enc" // 口令密文（iv+ct 同串 base64）

    /** 当前 E2EE 口令明文；未设置返回空串（明文同步）。 */
    fun passphrase(ctx: android.content.Context): String {
        val sp = ctx.getSharedPreferences(PREFS, android.content.Context.MODE_PRIVATE)
        val main = sp.getString(KEY_MAIN, null) ?: return ""
        val enc = sp.getString(KEY_PASS_ENC, null) ?: return ""
        return try {
            val raw = Base64.decode(enc, Base64.NO_WRAP)
            val key = Base64.decode(main, Base64.NO_WRAP)
            val iv = raw.copyOfRange(0, 12)
            val ct = raw.copyOfRange(12, raw.size)
            val c = Cipher.getInstance("AES/GCM/NoPadding").apply {
                init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, iv))
            }
            String(c.doFinal(ct), Charsets.UTF_8)
        } catch (e: Exception) { "" }
    }

    /** 保存 E2EE 口令（主密钥加密落盘）；空串 = 清除（回到明文同步）。 */
    fun setPassphrase(ctx: android.content.Context, passphrase: String) {
        val sp = ctx.getSharedPreferences(PREFS, android.content.Context.MODE_PRIVATE).edit()
        if (passphrase.isEmpty()) {
            sp.remove(KEY_MAIN).remove(KEY_PASS_ENC).apply()
            return
        }
        val mainKey = ByteArray(32).also(random::nextBytes)
        val iv = ByteArray(12).also(random::nextBytes)
        val c = Cipher.getInstance("AES/GCM/NoPadding").apply {
            init(Cipher.ENCRYPT_MODE, SecretKeySpec(mainKey, "AES"), GCMParameterSpec(128, iv))
        }
        val ct = c.doFinal(passphrase.toByteArray(Charsets.UTF_8))
        val blob = iv + ct
        sp.putString(KEY_MAIN, Base64.encodeToString(mainKey, Base64.NO_WRAP))
            .putString(KEY_PASS_ENC, Base64.encodeToString(blob, Base64.NO_WRAP))
            .apply()
    }

    // ---- 服务端通道行级封装：只加密 data 字段，entity_id/updated_at/deleted_at 保持明文（保留增量拉取与 LWW） ----

    /** data 字段封装：有口令返回密文字符串，无口令返回原 JSONObject（明文行，向后兼容）。 */
    fun sealRow(data: JSONObject, passphrase: String): Any =
        if (passphrase.isEmpty()) data else encrypt(data.toString(), passphrase)

    /** data 字段解封：密文字符串解出 JSONObject；明文行原样；口令不对（解不开）返回 null → 跳过该行。 */
    fun openRow(v: Any?, passphrase: String): JSONObject? = when (v) {
        is JSONObject -> v
        is String -> {
            val plain = if (isEncrypted(v)) tryDecrypt(v, passphrase) ?: return null else v
            try { JSONObject(plain) } catch (e: Exception) { null }
        }
        else -> null
    }
}
