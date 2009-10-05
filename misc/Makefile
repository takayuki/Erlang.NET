topdir = ..
srcdir = .

MONO = mono
MONOFLAGS =
MCS = gmcs
MCSFLAGS = -debug -warn:4 -clscheck+ -warnaserror+ \
  -lib:$(topdir)/bin -r:System.Configuration -r:log4net

.SUFFIXES: .exe .dll .cs

.cs.dll:
	
SRCS = \
BigInteger/BigInteger.cs \
AbstractConnection.cs \
AbstractNode.cs \
BufferedTcpClient.cs \
GenericQueue.cs \
Link.cs \
Links.cs \
OtpActor.cs \
OtpActorMbox.cs \
OtpActorSched.cs \
OtpAuthException.cs \
OtpConnection.cs \
OtpCookedConnection.cs \
OtpEpmd.Client.cs \
OtpEpmd.Server.cs \
OtpErlangAtom.cs \
OtpErlangBinary.cs \
OtpErlangBitstr.cs \
OtpErlangBoolean.cs \
OtpErlangByte.cs \
OtpErlangChar.cs \
OtpErlangDecodeException.cs \
OtpErlangDouble.cs \
OtpErlangException.cs \
OtpErlangExit.cs \
OtpErlangExternalFun.cs \
OtpErlangFloat.cs \
OtpErlangFun.cs \
OtpErlangInt.cs \
OtpErlangList.cs \
OtpErlangLong.cs \
OtpErlangObject.cs \
OtpErlangPid.cs \
OtpErlangPort.cs \
OtpErlangRangeException.cs \
OtpErlangRef.cs \
OtpErlangShort.cs \
OtpErlangString.cs \
OtpErlangTuple.cs \
OtpErlangUInt.cs \
OtpErlangUShort.cs \
OtpException.cs \
OtpExternal.cs \
OtpInputStream.cs \
OtpLocalNode.cs \
OtpMD5.cs \
OtpMbox.cs \
OtpMsg.cs \
OtpNode.cs \
OtpNodeStatus.cs \
OtpOutputStream.cs \
OtpPeer.cs \
OtpSelf.cs \
OtpServer.cs \
ThreadBase.cs

all: echo.exe epmd.exe

Erlang.dll: $(SRCS)
	$(MCS) $(MCSFLAGS) -target:library -out:Erlang.dll \
	  $(SRCS)

epmd.exe: Erlang.dll epmd.cs
	$(MCS) $(MCSFLAGS) -r:Erlang -target:exe -out:epmd.exe epmd.cs

ping.exe: Erlang.dll ping.cs
	$(MCS) $(MCSFLAGS) -r:Erlang -target:exe -out:ping.exe ping.cs

echo.exe: Erlang.dll echo.cs
	$(MCS) $(MCSFLAGS) -r:Erlang -target:exe -out:echo.exe echo.cs

clean:
	rm -f *.dll *.exe *.mdb *~
