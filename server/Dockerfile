FROM php:8.5-apache@sha256:4ae5bf39475b9af7d4812dae2da4e820a9320950658a7dd8145042d93cb08389
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80