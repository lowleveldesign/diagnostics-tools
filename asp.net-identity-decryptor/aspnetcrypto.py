# -*- coding: utf-8 -*-
import base64
import logging
import argparse
import struct
import gzip
from StringIO import StringIO
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import hashes, padding, hmac
from cryptography.hazmat.backends import default_backend


def derivekey(key, label, context, keyLengthInBits):
    lblcnt = 0 if None == label else len(label)
    ctxcnt = 0 if None == context else len(context)
    buffer = ['\x00'] * (4 + lblcnt + 1 + ctxcnt + 4)
    if lblcnt != 0:
        buffer[4:(4 + lblcnt)] = label
    if ctxcnt != 0:
        buffer[(5 + lblcnt):(5 + lblcnt + ctxcnt)] = context
    _writeuint(keyLengthInBits, buffer, 5 + lblcnt + ctxcnt)
    dstoffset = 0
    v = keyLengthInBits / 8
    res = ['\x00'] * v
    num = 1
    while v > 0:
        _writeuint(num, buffer, 0)
        h = hmac.HMAC(key, hashes.SHA512(), backend=default_backend())
        h.update(''.join(buffer))
        hash = h.finalize()
        cnt = min(v, len(hash))
        res[dstoffset:cnt] = hash[0:cnt]
        dstoffset += cnt
        v -= cnt
        num += 1
    return ''.join(res)

def _writeuint(v, buf, offset):
    buf[offset:(offset + 4)] = struct.pack('>I', v)

def _tokendecode(aspnetstr):
    if len(aspnetstr) < 1:
        raise ValueError('Invalid input')

    # add padding if necessary - last character of the string defines the padding length
    num = ord(aspnetstr[-1]) - 48
    if num < 0 or num > 10:
        return None

    return base64.urlsafe_b64decode(aspnetstr[:-1] + num * '=')

def _decode(aspnetstr):
    # add padding if necessary
    pad = 3 - ((len(args.aspnetstr) + 3) % 4)
    if pad != 0:
        aspnetstr += pad * '='
    return base64.urlsafe_b64decode(aspnetstr)

def decrypt(dkey, b):
    # extract initialization vector (256 bit)
    iv = b[0:16]
    decryptor = Cipher(algorithms.AES(dkey), modes.CBC(iv), backend=default_backend()).decryptor()
    unpadder = padding.PKCS7(algorithms.AES.block_size).unpadder()

    ciphertext = b[16:-32]
    text_padded = decryptor.update(ciphertext) + decryptor.finalize()
    return unpadder.update(text_padded) + unpadder.finalize()

if __name__ == '__main__':
    # Turn on Logging
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s %(message)s')

    parser = argparse.ArgumentParser('ASP.NET encryptor/decryptor')
    parser.add_argument('aspnetstr', metavar='aspnet-text', help='ASP.NET encrypted text')
    parser.add_argument('-skey', required=True, help='Symmetric key for AES encryption/decryption')
    parser.add_argument('-enctype', required=False, help='Type of action that generated the given encryption text (owinauth or antiforgery)')
    args = parser.parse_args()

    skey = args.skey.decode('hex')
    label = None
    context = None
    compressed = False
    encrypted = None
    if args.enctype == 'owinauth':
        label = b'>Microsoft.Owin.Security.Cookies.CookieAuthenticationMiddleware\x11ApplicationCookie\x02v1'
        context = b'User.MachineKey.Protect'
        compressed = True
        encrypted = _decode(args.aspnetstr)
    elif args.enctype == 'antiforgery':
        label = b'/System.Web.Helpers.AntiXsrf.AntiForgeryToken.v1'
        context = b'User.MachineKey.Protect'
        encrypted = _tokendecode(args.aspnetstr)

    dkey = derivekey(skey, context, label, 256)
    decrypted = decrypt(dkey, encrypted)

    if compressed:
        decrypted = gzip.GzipFile(fileobj=StringIO(decrypted)).read()

    print "%s %s" % (decrypted.encode('hex'), decrypted)
