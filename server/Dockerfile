FROM php:8.4-apache@sha256:f75d6b8ab2288b46bb5a8b654487f873117f279531dbf33a6c4bff136dfdb340
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80