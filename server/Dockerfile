FROM php:8.4-apache@sha256:30cf37c4a2680da1f7c00e044eb515dda1a875f4a553384a1289bf5e80acd40b
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80