������ "NetBox" ��� Far Manager 2.0
***********************************

FTP/FTPS/SFTP/WebDAV ������ ��� Far.

1. ����� �������� � �������
   ������ ��������� ���������� ����� ���������� FTP, FTPS, SFTP � WebDAV.
   FTP � WebDAV ���������� �� ���������� libcurl (http://curl.haxx.se).
   SFTP ���������� �� ���� ���������� libssh2 (http://www.libssh2.org).
   ������ xml �������� ����� ���������� TinyXML
   (http://sourceforge.net/projects/tinyxml).

2. ������������� �������� ��������� ������
   ������ � ��������� ������� �������� ��� ����� ����������� ���������
   ������, ��� � ����� �������.
   �������� ��� �������� ������������� ��������:
     a. NetBox:Protocol://[[User]:[Password]@]HostName[:Port][/Path]
        ��� Protocol - ��� ��������� (ftp/ftps/sftp/http/https)
            User - ��� ������������
            Password - ������ ������������
            HostName - ��� �����
            Port - ����� �����
            Path - ����

     b. (ftp|sftp|http|https)://[[User]:[Password]@]HostName[:Port][/Path]
        ��� (ftp|sftp|http|https) - ��� ���������
            User - ��� ������������
            Password - ������ ������������
            HostName - ��� �����
            Port - ����� �����
            Path - ����

   ��������, ��������� ������� � Far'e �������� ������������� ���������
   svn � ��������� ������ Far:
     a. NetBox: http://farmanager.com/svn/trunk
     b. http://farmanager.com/svn/trunk

3. ����� ��� ������������� ��������� SFTP
   ���� � ������� (��������� � ���������) ������ ���� � ������� OpenSSL.
   ���� �� ����������� putty ��� ���������� �� ��� WinSCP, ��c����������
   ����� ���������� puttigen �� ������� Putty � ������ OpenSSL.

4. ����
   ���������� ������ � ������:
   Ctrl+Alt+Ins: ����������� �������� URL � ����� ������ (������ � �������).
   Shift+Alt+Ins: ����������� �������� URL � ����� ������ (��� ������).

5. ���������:
   ���������� ���������� ������ � ������� �������� Far (...Far\Plugins).

6. ������� �� �������:
   ������ ������ ��������������� "as is" ("��� ����"). ����� �� ����� 
   ��������������� �� ����������� ������������� ������� �������.

���� ������� (artemsen@gmail.com)
              http://code.google.com/p/farplugs
������ ������� (michael.lukashov@gmail.com)
               https://github.com/michaellukashov/Far-NetBox
