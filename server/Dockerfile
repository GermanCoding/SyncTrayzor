FROM php:8.4-apache@sha256:895001a3ae5e48ce21952cdb517e424e484aeadbfceb071145fc17aab5172dd0
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80