FROM php:8.5-apache@sha256:103a8cd59a029b387470deace9736a7bc5aeb167a746774c68da82bb469c578c
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80