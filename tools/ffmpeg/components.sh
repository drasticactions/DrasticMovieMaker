#!/bin/sh
# The FFmpeg components every build enables, as configure flags. One definition shared by build-desktop.sh and the
# manifest test (tests compare out/<target>/manifest.txt with what import accepts, MediaFormats.cs), so import and
# publish behave the same on every target.
#
# Pure: prints the flags and nothing else.
#
# With a custom AVIOContext (MediaStreamSource) FFmpeg skips AVFMT_NOFILE demuxers such as image2, so every picture
# decoder has its image_*_pipe demuxer here; image2 stays for completeness.

# Video decoders: the legacy import formats (ASF/WMV, AVI incl. DV, MPEG-1/2) and modern ones.
VIDEO_DECODERS="h264,hevc,mpeg1video,mpeg2video,mpeg4,msmpeg4v1,msmpeg4v2,msmpeg4v3,wmv1,wmv2,wmv3,vc1,\
vp8,vp9,libdav1d,theora,dvvideo,mjpeg,flv,h263,h263p,rawvideo,prores,dnxhd,huffyuv,ffv1,cinepak,msvideo1,\
indeo3,indeo4,indeo5,svq1,svq3,utvideo,qtrle,png"

# Picture decoders (MediaFormats.Pictures). HEIC/HEIF use hevc, AVIF uses libdav1d (both through mov).
PICTURE_DECODERS="bmp,gif,mjpeg,png,tiff,webp,jpeg2000"

AUDIO_DECODERS="aac,aac_latm,mp1,mp1float,mp2,mp2float,mp3,mp3float,wmav1,wmav2,wmapro,wmalossless,wmavoice,\
flac,vorbis,opus,alac,ac3,eac3,pcm_s8,pcm_u8,pcm_s16le,pcm_s16be,pcm_s24le,pcm_s24be,pcm_s32le,pcm_s32be,\
pcm_f32le,pcm_f32be,pcm_f64le,pcm_f64be,pcm_mulaw,pcm_alaw,adpcm_ima_wav,adpcm_ms,gsm_ms,dvaudio"

DEMUXERS="asf,avi,dv,mpegps,mpegvideo,mpegts,mov,matroska,ogg,flv,wav,w64,mp3,aiff,au,aac,ac3,flac,caf,\
h264,hevc,m4v,mjpeg,gif,image2,image_bmp_pipe,image_gif_pipe,image_jpeg_pipe,image_png_pipe,image_tiff_pipe,\
image_webp_pipe,image_j2k_pipe,loas,pcm_s16le"

PARSERS="h264,hevc,mpegvideo,mpeg4video,vc1,vp8,vp9,av1,aac,aac_latm,mpegaudio,ac3,flac,opus,vorbis,mjpeg,\
png,bmp,gif,webp,dnxhd,dvaudio,h263,jpeg2000"

# Publish (MP4 = x264 + AAC, WebM = VP9 + Opus), lossless parity (FFV1 + PCM in Matroska) and FLAC narration.
ENCODERS="libx264,libvpx_vp9,libopus,aac,ffv1,pcm_s16le,flac,png"
MUXERS="mp4,mov,webm,matroska,flac,wav"

PROTOCOLS="file"

printf '%s\n' "--disable-everything \
--enable-decoder=$VIDEO_DECODERS,$PICTURE_DECODERS,$AUDIO_DECODERS \
--enable-demuxer=$DEMUXERS \
--enable-parser=$PARSERS \
--enable-encoder=$ENCODERS \
--enable-muxer=$MUXERS \
--enable-protocol=$PROTOCOLS \
--enable-gpl --enable-version3 \
--enable-libx264 --enable-libvpx --enable-libopus --enable-libdav1d --enable-zlib"
