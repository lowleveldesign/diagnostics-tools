# -*- coding: utf-8 -*- 
'''
Created on 14/07/2014

@author: Aitor Gomez Goiri
'''

import struct
from cryptography.hazmat.primitives import hashes, hmac
from cryptography.hazmat.backends import default_backend

class NIST(object):
    
    def _get_reseted_hmac(self):
        return hmac.HMAC(self.secret, hashes.SHA512(), backend=default_backend())
        
    def set_hmac(self, secret):
        assert secret != None, "Key derivation key cannot be null."
        self.secret = secret

    # Calculate the size of a key. The key size is given in bits, but we can
    # only allocate them by octets (i.e., bytes), so make sure we round up to
    # the next whole number of octets to have room for all the bits. For
    # example, a key size of 9 bits would require 2 octets to store it.
    # @param ks
    #    The key size, in bits.
    # @return The key size, in octets, large enough to accommodate {@code ks}
    #         bits.
    def _calc_key_size(self, ks):
        assert ks > 0, "Key size must be > 0 bits."
        n = ks / 8
        rem = ks % 8
        return n if rem==0 else n+1
    
    def _to_one_byte(self, inByte):
        assert isinstance( inByte, int ), "This method expected an int as a parameter"
        assert inByte<128, "The maximum value of ctr is 127 (1 byte only)"
        return struct.pack('B', inByte)

    def _debug_string_as_bytes(self, array_alpha):
        import binascii
        print binascii.hexlify(array_alpha)
    
    def derive_key(self, outputSizeBits, fixedInput):
        assert outputSizeBits >= 56, "Key has size of %d, which is less than minimum of 56-bits." % outputSizeBits
        assert (outputSizeBits % 8) == 0, "Key size (%d) must be a even multiple of 8-bits." % outputSizeBits
        
        outputSizeBytes = self._calc_key_size(outputSizeBits); # Safely convert to whole # of bytes.
        derivedKey = [] # bytearray() (better to use this?)
                
        # Repeatedly call of HmacSHA1 hash until we've collected enough bits
        # for the derived key.
        ctr = 1 # Iteration counter for NIST 800-108
        totalCopied = 0
        destPos = 0
        lenn = 0
        tmpKey = None
        
        while True: # ugly translation of do-while
            hmac = self._get_reseted_hmac() 
            hmac.update( self._to_one_byte(ctr) )
            ctr += 1 # note that the maximum value of ctr is 127 (1 byte only)
            
            hmac.update(fixedInput)
            tmpKey = hmac.finalize() # type: string
            #print self._debug_string_as_bytes(tmpKey)
            
            if len(tmpKey) >= outputSizeBytes:
                lenn = outputSizeBytes
            else:
                lenn = min(len(tmpKey), outputSizeBytes - totalCopied)
            
            #System.arraycopy(tmpKey, 0, derivedKey, destPos, lenn);
            derivedKey[destPos:destPos+lenn] = tmpKey[:lenn]
            totalCopied += len(tmpKey)
            destPos += lenn
            
            if totalCopied >= outputSizeBytes: # ugly translation of do-while
                break
            
            #print ''.join([x.encode("hex") for x in derivedKey]) #[hex(x) for x in derivedKey]
        
        return bytearray( derivedKey )